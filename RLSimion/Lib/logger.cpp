﻿#include "logger.h"
#include "worlds/world.h"
#include "../Common/named-var-set.h"
#include "config.h"
#include "stats.h"
#include "../../tools/System/Timer.h"
#include "../../tools/System/FileUtils.h"
#include "../../tools/System/CrossPlatform.h"
#include "app.h"
#include "utils.h"
#include "experiment.h"
#include <algorithm>

FILE *Logger::m_logFile = 0;
MessageOutputMode Logger::m_messageOutputMode = MessageOutputMode::Console;
NamedPipeClient Logger::m_outputPipe;
bool Logger::m_bLogMessagesEnabled = true;

#define HEADER_MAX_SIZE 16
#define EXPERIMENT_HEADER 1
#define EPISODE_HEADER 2
#define STEP_HEADER 3
#define EPISODE_END_HEADER 4

//we pack every int/double as 64bit data to avoid struct-padding issues (the size of the struct might not be the same in C++ and C#

struct ExperimentHeader
{
	long long int magicNumber = EXPERIMENT_HEADER;
	long long int fileVersion = Logger::BIN_FILE_VERSION;
	long long int numEpisodes = 0;

	long long int padding[HEADER_MAX_SIZE - 3]; //extra space
	ExperimentHeader()
	{
		memset(padding, 0, sizeof(padding));
	}
};

struct EpisodeHeader
{
	long long int magicNumber = EPISODE_HEADER;
	long long int episodeType;
	long long int episodeIndex;
	long long int numVariablesLogged;

	//Added in version 2: if the episode belongs to an evaluation, the number of episodes per evaluation might be >1
	//the episodeSubIndex will be in [1..numEpisodesPerEvaluation]
	long long int episodeSubIndex;

	long long int padding[HEADER_MAX_SIZE - 5]; //extra space
	EpisodeHeader()
	{
		memset(padding, 0, sizeof(padding));
	}
};

struct StepHeader
{
	long long int magicNumber = STEP_HEADER;
	long long int stepIndex;

	double experimentRealTime;
	double episodeSimTime;
	double episodeRealTime;

	long long int padding[HEADER_MAX_SIZE - 5]; //extra space
	StepHeader()
	{
		memset(padding, 0, sizeof(padding));
	}
};

Logger::Logger(ConfigNode* pConfigNode)
{
	if (!pConfigNode) return;

	m_bLogEvaluationEpisodes = BOOL_PARAM(pConfigNode, "Log-Eval-Episodes", "Log evaluation episodes?", true);

	m_bLogTrainingEpisodes = BOOL_PARAM(pConfigNode, "Log-Training-Episodes", "Log training episodes?", false);

	m_logFreq = DOUBLE_PARAM(pConfigNode, "Log-Freq", "Log frequency. Simulation time in seconds.", 0.25);

	m_bLogFunctions = BOOL_PARAM(pConfigNode, "Log-Functions", "Log functions learned?", true);
	m_numFunctionLogPoints = INT_PARAM(pConfigNode, "Num-Functions-Logged", "How many times per experiment save logged functions", 10);

	m_pEpisodeTimer = new Timer();
	m_pExperimentTimer = new Timer();
	m_lastLogSimulationT = 0.0;
}

#define LOG_DESCRIPTOR_EXTENSION ".log"
#define LOG_BINARY_EXTENSION ".log.bin"
#define FUNCTION_LOG_BINARY_EXTENSION ".log.functions"

void Logger::setOutputFilenames()
{
	string inputConfigFile = removeExtension(SimionApp::get()->getConfigFile());

	//we register the names of the log files for input/output stuff
	m_outputLogDescriptor = inputConfigFile + LOG_DESCRIPTOR_EXTENSION;
	SimionApp::get()->registerOutputFile(m_outputLogDescriptor.c_str());
	m_outputLogBinary = inputConfigFile + LOG_BINARY_EXTENSION;
	SimionApp::get()->registerOutputFile(m_outputLogBinary.c_str());

	//open the log file
	openLogFile(m_outputLogBinary.c_str());

	if (m_bLogFunctions.get())
	{
		m_outputFunctionLogBinary = inputConfigFile + FUNCTION_LOG_BINARY_EXTENSION;
		SimionApp::get()->registerOutputFile(m_outputFunctionLogBinary.c_str());
	}
}


Logger::~Logger()
{
	if (m_pExperimentTimer) delete m_pExperimentTimer;
	if (m_pEpisodeTimer) delete m_pEpisodeTimer;

	//Send message to let the server know we have finished
	//Not really needed under Windows, but it seems to be needed in Linux
	const char closingMessage [] = "<End></End>";
	m_outputPipe.writeBuffer(closingMessage, (int)strlen(closingMessage) + 1);
	m_outputPipe.closeConnection();

	for (auto it = m_stats.begin(); it != m_stats.end(); it++)
		delete *it;

	closeLogFile();
	closeFunctionLogFile();
}


bool Logger::isEpisodeTypeLogged(bool evalEpisode)
{
	return (evalEpisode && m_bLogEvaluationEpisodes.get()) || (!evalEpisode && m_bLogTrainingEpisodes.get());
}



void Logger::writeLogFileXMLDescriptor(const char* filename)
{
	char buffer[BUFFER_SIZE];

	FILE * logXMLDescriptorFile;
	CrossPlatform::Fopen_s(&logXMLDescriptorFile, filename, "w");
	if (logXMLDescriptorFile)
	{
		if (m_bLogFunctions.get())
			CrossPlatform::Sprintf_s(buffer, BUFFER_SIZE, "<ExperimentLogDescriptor BinaryDataFile=\"%s\" FunctionsDataFile=\"%s\" SceneFile=\"%s\">\n"
				, getFilename(m_outputLogBinary).c_str()
				, getFilename(m_outputFunctionLogBinary).c_str()
				, (SimionApp::get()->pWorld->getDynamicModel()->getName() + string(".scene")).c_str());
		else
			CrossPlatform::Sprintf_s(buffer, BUFFER_SIZE, "<ExperimentLogDescriptor BinaryDataFile=\"%s\" SceneFile=\"%s\">\n"
				, getFilename(m_outputLogBinary).c_str()
				, (SimionApp::get()->pWorld->getDynamicModel()->getName() + string(".scene")).c_str());
		writeEpisodeTypesToBuffer(buffer);
		writeNamedVarSetDescriptorToBuffer(buffer, "State", SimionApp::get()->pWorld->getDynamicModel()->getStateDescriptorPtr()); //state
		writeNamedVarSetDescriptorToBuffer(buffer, "Action", SimionApp::get()->pWorld->getDynamicModel()->getActionDescriptorPtr()); //action
		writeNamedVarSetDescriptorToBuffer(buffer, "Reward", SimionApp::get()->pWorld->getRewardVector()->getDescriptorPtr());
		writeStatDescriptorToBuffer(buffer);
		CrossPlatform::Strcat_s(buffer, BUFFER_SIZE, "</ExperimentLogDescriptor>");
		fwrite(buffer, 1, strlen(buffer), logXMLDescriptorFile);

		fclose(logXMLDescriptorFile);
	}
	else logMessage(MessageType::Warning, "Couldn't save experiment log descriptor");
}

void Logger::writeEpisodeTypesToBuffer(char* pOutBuffer)
{
	if (m_bLogEvaluationEpisodes.get()) CrossPlatform::Strcat_s(pOutBuffer, BUFFER_SIZE
		, "  <Episode-Type Id=\"0\">Evaluation</Episode-Type>\n");
	if (m_bLogTrainingEpisodes.get()) CrossPlatform::Strcat_s(pOutBuffer, BUFFER_SIZE
		, "  <Episode-Type Id=\"1\">Training</Episode-Type>\n");
}

void Logger::writeStatDescriptorToBuffer(char* pOutBuffer)
{
	char buffer[BUFFER_SIZE];

	for (auto iterator = m_stats.begin(); iterator != m_stats.end(); iterator++)
	{
		CrossPlatform::Sprintf_s(buffer, BUFFER_SIZE, "  <Stat-variable>%s/%s</Stat-variable>\n", (*iterator)->getKey().c_str()
			, (*iterator)->getSubkey().c_str());
		CrossPlatform::Strcat_s(pOutBuffer, BUFFER_SIZE, buffer);
	}
}
void Logger::writeNamedVarSetDescriptorToBuffer(char* pOutBuffer, const char* id, const Descriptor* descriptor)
{
	string circular;
	char buffer[BUFFER_SIZE];
	for (unsigned int i = 0; i < descriptor->size(); i++)
	{
		if ((*descriptor)[i].isCircular())
			circular = "true";
		else
			circular = "false";
		CrossPlatform::Sprintf_s(buffer, BUFFER_SIZE, "  <%s-variable Min=\"%.2f\" Max=\"%.2f\" Circular=\"%s\" Units=\"%s\">%s</%s-variable>\n"
			, id, (*descriptor)[i].getMin(), (*descriptor)[i].getMax(), circular.c_str(), (*descriptor)[i].getUnits()
			, (*descriptor)[i].getName(), id);
		CrossPlatform::Strcat_s(pOutBuffer, BUFFER_SIZE, buffer);
	}
}

void Logger::firstEpisode()
{
	//set episode start time
	m_pEpisodeTimer->start();

	//generate the xml descriptor of the log file
	writeLogFileXMLDescriptor(m_outputLogDescriptor.c_str());
	//write the log file header
	writeExperimentHeader();

	//write the function log header
	if (areFunctionsLogged())
		openFunctionLogFile(m_outputFunctionLogBinary.c_str());
}

void Logger::lastEpisode()
{
}

void Logger::firstStep()
{
	//initialise the episode reward
	m_episodeRewardSum = 0.0;

	//set episode start time
	m_pEpisodeTimer->start();

	m_lastLogSimulationT = 0.0;

	bool bEvalEpisode = SimionApp::get()->pExperiment->isEvaluationEpisode();

	//reset stats
	for (auto it = m_stats.begin(); it != m_stats.end(); it++) (*it)->reset();

	//write the episode header in the log file
	if (isEpisodeTypeLogged(bEvalEpisode))
		writeEpisodeHeader();

	//log all the functions if need to
	if (areFunctionsLogged())
	{
		size_t functionLogFreq = 1;
		if (m_numFunctionLogPoints.get() > 0)
		{
			if ((unsigned int) m_numFunctionLogPoints.get() < SimionApp::get()->pExperiment->getTotalNumEpisodes())
				functionLogFreq = SimionApp::get()->pExperiment->getTotalNumEpisodes() / m_numFunctionLogPoints.get();
			else
				functionLogFreq = 1;
		}

		if ((SimionApp::get()->pExperiment->getEpisodeIndex()-1) % functionLogFreq == 0)
			writeFunctionLogSample();
	}
}

void Logger::lastStep()
{
	Experiment* pExperiment = SimionApp::get()->pExperiment.ptr();
	bool bEvalEpisode = pExperiment->isEvaluationEpisode();
	if (!isEpisodeTypeLogged(bEvalEpisode)) return;

	//log the end of the episode: this way we don't have to precalculate the number of steps logged per episode
	writeEpisodeEndHeader();

	//in case this is the last step of an evaluation episode, we log it and send the info to the host if there is one
	char buffer[BUFFER_SIZE];
	int numEvaluations = pExperiment->getNumEvaluations();
	int numEpisodesPerEvaluation = pExperiment->getNumEpisodesPerEvaluation();
	int numRelativeEpisodeIndex = pExperiment->getRelativeEpisodeIndex();

	//log the progress if an evaluation episode has ended
	if (pExperiment->isEvaluationEpisode()
		&& pExperiment->getEpisodeInEvaluationIndex() == pExperiment->getNumEpisodesPerEvaluation())
	{
		CrossPlatform::Sprintf_s(buffer, BUFFER_SIZE, "%f,%f"
			, (double)(numRelativeEpisodeIndex - 1)	/ (std::max(1.0, (double)numEvaluations*numEpisodesPerEvaluation - 1))
			, m_episodeRewardSum / (double)pExperiment->getStep());
		logMessage(MessageType::Evaluation, buffer);
	}
}

void Logger::timestep(State* s, Action* a, State* s_p, Reward* r)
{
	bool bEvalEpisode = SimionApp::get()->pExperiment->isEvaluationEpisode();
	//we add the scalar reward in evaluation episodes for monitoring purposes, no matter if we are logging this type of episode or not
	if (bEvalEpisode) m_episodeRewardSum += r->getSumValue();

	//update experiment stats
	for (auto iterator = m_stats.begin(); iterator != m_stats.end(); iterator++)
	{
		(*iterator)->addSample();
	}

	if (!isEpisodeTypeLogged(bEvalEpisode)) return;

	//output episode log data
	if (SimionApp::get()->pWorld->getStepStartSimTime() - m_lastLogSimulationT >= m_logFreq.get()
		|| SimionApp::get()->pExperiment->isFirstStep() || SimionApp::get()->pExperiment->isLastStep())
	{
		writeStepData(s, a, s_p, r);
		//reset stats
		for (auto it = m_stats.begin(); it != m_stats.end(); it++) (*it)->reset();
		m_lastLogSimulationT = SimionApp::get()->pWorld->getStepStartSimTime();
	}
}

void Logger::writeStepData(State* s, Action* a, State* s_p, Reward* r)
{
	int offset = 0;
	char buffer[BUFFER_SIZE];
	buffer[0] = 0;

	offset += writeStepHeaderToBuffer(buffer, offset);

	//We log s_p instead of s to log a coherent state-reward: r= f(s_p)
	offset += writeNamedVarSetToBuffer(buffer, offset, s_p);
	offset += writeNamedVarSetToBuffer(buffer, offset, a);
	offset += writeNamedVarSetToBuffer(buffer, offset, r);
	offset += writeStatsToBuffer(buffer, offset);

	writeLogBuffer(buffer, offset);
}

void Logger::writeExperimentHeader()
{
	ExperimentHeader header;
	Experiment* pExperiment = SimionApp::get()->pExperiment.ptr();

	if (m_bLogEvaluationEpisodes.get())
		header.numEpisodes +=
		pExperiment->getNumEvaluations()*pExperiment->getNumEpisodesPerEvaluation();
	if (m_bLogTrainingEpisodes.get())
		header.numEpisodes += pExperiment->getNumTrainingEpisodes();

	writeLogBuffer((char*)&header, sizeof(ExperimentHeader));
}

void Logger::writeEpisodeHeader()
{
	EpisodeHeader header;
	Experiment* pExperiment = SimionApp::get()->pExperiment.ptr();
	World* pWorld = SimionApp::get()->pWorld.ptr();

	header.episodeIndex = pExperiment->getRelativeEpisodeIndex();
	if (pExperiment->isEvaluationEpisode())
		header.episodeSubIndex = pExperiment->getEpisodeInEvaluationIndex();
	else
		header.episodeSubIndex = 1; // training episodes cannot have sub-episodes
	header.episodeType = (pExperiment->isEvaluationEpisode() ? 0 : 1);
	header.numVariablesLogged =
		pWorld->getDynamicModel()->getActionDescriptor().size()
		+ pWorld->getDynamicModel()->getStateDescriptor().size()
		+ pWorld->getRewardVector()->getNumVars()
		+ m_stats.size();

	writeLogBuffer((char*)&header, sizeof(EpisodeHeader));
}

void Logger::writeEpisodeEndHeader()
{
	StepHeader episodeEndHeader;
	memset(&episodeEndHeader, 0, sizeof(StepHeader));
	episodeEndHeader.magicNumber = EPISODE_END_HEADER;
	writeLogBuffer((char*)&episodeEndHeader, sizeof(StepHeader));
}

int Logger::writeStepHeaderToBuffer(char* buffer, int offset)
{
	StepHeader header;
	header.stepIndex = SimionApp::get()->pExperiment->getStep();
	header.episodeRealTime = m_pEpisodeTimer->getElapsedTime();
	header.episodeSimTime = SimionApp::get()->pWorld->getEpisodeSimTime();
	header.experimentRealTime = m_pExperimentTimer->getElapsedTime();

	CrossPlatform::Memcpy_s(buffer + offset, BUFFER_SIZE, (char*)&header, sizeof(StepHeader));

	return sizeof(header);
}

int Logger::writeNamedVarSetToBuffer(char* buffer, int offset, const NamedVarSet* pNamedVarSet)
{
	size_t numVars = pNamedVarSet->getNumVars();
	double* pDoubleBuffer = (double*)(buffer + offset);
	for (size_t i = 0; i < numVars; ++i)
		pDoubleBuffer[i] = pNamedVarSet->get(i);
	return (int) (numVars * sizeof(double));
}

int Logger::writeStatsToBuffer(char* buffer, int offset)
{
	int numVars = (int)m_stats.size();
	double* pDoubleBuffer = (double*)(buffer + offset);
	int i = 0;
	for (auto it = m_stats.begin(); it != m_stats.end(); ++it)
	{
		//Because we may not be logging all the steps, we need to save the average value from the last logged step
		//instead of only the current value
		pDoubleBuffer[i] = (*it)->getStatsInfo()->getAvg();
		++i;
	}
	return (int) (numVars * sizeof(double));
}


void Logger::addVarSetToStats(const char* key, NamedVarSet* varset)
{
	for (int i = 0; i < (int) varset->getNumVars(); i++)
	{
		m_stats.push_back(new Stats<double>(key, varset->getProperties(i)->getName(), varset->getRef(i)));
	}
}

size_t Logger::getNumStats()
{
	return m_stats.size();
}

IStats* Logger::getStats(unsigned int i)
{
	if (i < m_stats.size())
		return m_stats[i];
	return nullptr;
}


void Logger::openLogFile(const char* logFilename)
{
	CrossPlatform::Fopen_s(&m_logFile, logFilename, "wb");
	if (!m_logFile)
		logMessage(MessageType::Warning, "Log file couldn't be opened, so no log info will be saved.");
}
void Logger::closeLogFile()
{
	if (m_logFile)
		fclose(m_logFile);
}

void Logger::writeLogBuffer(const char* pBuffer, int numBytes)
{
	if (m_logFile)
		fwrite(pBuffer, 1, numBytes, m_logFile);
}

void Logger::enableLogMessages(bool enable)
{
	m_bLogMessagesEnabled = enable;
}

void Logger::logMessage(MessageType type, const char* message)
{
	char messageLine[1024];

	if (m_messageOutputMode == MessageOutputMode::NamedPipe && m_outputPipe.isConnected())
	{
		switch (type)
		{
		case Warning:
			CrossPlatform::Sprintf_s(messageLine, 1024, "<Message>WARNING: %s</Message>", message); break;
		case Progress:
			CrossPlatform::Sprintf_s(messageLine, 1024, "<Progress>%s</Progress>", message); break;
		case Evaluation:
			CrossPlatform::Sprintf_s(messageLine, 1024, "<Evaluation>%s</Evaluation>", message); break;
		case Info:
			CrossPlatform::Sprintf_s(messageLine, 1024, "<Message>%s</Message>", message); break;
		case Error:
			CrossPlatform::Sprintf_s(messageLine, 1024, "<Error>ERROR: %s</Error>", message); break;
		}
		m_outputPipe.writeBuffer(messageLine, (int)strlen(messageLine) + 1);
	}
	else if (m_bLogMessagesEnabled)
	{
		switch (type)
		{
		case Warning:
			printf("WARNING: %s\n", message); break;
		case Progress:
			//extra spaces to avoid overwriting only partially previous message
			printf("PROGRESS: %s                     \r", message); break;
		case Evaluation:
			//extra spaces to avoid overwriting only partially previous message
			printf("EVALUATION: %s\n", message); break;
		case Info:
			printf("%s\n", message); break;
		case Error:
			printf("ERROR: %s\n", message); break;
		}
	}
	if (type == MessageType::Error)
		throw std::runtime_error(message);
}
