#pragma once
#ifdef _WIN64

#ifdef BUILD_CNTK_WRAPPER
//we are building the CNTK Wrapper
#define DLL_API __declspec(dllexport)
#else
#define DLL_API
//#define CNTK_HEADERONLY_DEFINITIONS
#endif

#include <vector>
#include <unordered_map>
#include <memory>

using namespace std;
namespace tinyxml2 { class XMLElement; }
namespace CNTK
{ 
	class Function;
	typedef std::shared_ptr<Function> FunctionPtr;

	class Trainer;
	typedef std::shared_ptr<Trainer> TrainerPtr;

	class Value;
	typedef std::shared_ptr<Value> ValuePtr;

	class Variable;
	class NDShape;
}
class CNetworkArchitecture;
class CInputData;
class CLinkConnection;
class COptimizerSetting;
class INetwork;

//Interface class
class IProblem
{
public:
	//virtual IProblem(tinyxml2::XMLElement* pNode)= 0;
	virtual void destroy() = 0;

	virtual CNetworkArchitecture* getNetworkArchitecture() = 0;
	virtual const std::vector<CInputData*>& getInputs() const = 0;
	virtual const CLinkConnection* getOutput() const = 0;
	virtual const COptimizerSetting* getOptimizerSetting() const = 0;

	virtual INetwork* createNetwork() = 0;
};

class INetwork
{
public:

	virtual void destroy()= 0;

	virtual size_t getTotalSize() = 0;

	virtual vector<CInputData*>& getInputs()= 0;
	virtual vector<CNTK::FunctionPtr>& getOutputsFunctionPtr()= 0;
	virtual vector<CNTK::FunctionPtr>& getFunctionPtrs()= 0;
	virtual CNTK::FunctionPtr getNetworkFunctionPtr()= 0;
	virtual CNTK::FunctionPtr getLossFunctionPtr()= 0;
	virtual CNTK::TrainerPtr getTrainer()= 0;
	virtual CNTK::Variable getTargetOutput()= 0;

	virtual void buildNetworkFunctionPtr(const COptimizerSetting* optimizer)= 0;
	virtual void save(string fileName)= 0;

	virtual INetwork* cloneNonTrainable() const= 0;

	virtual void train(std::unordered_map<std::string, std::vector<double>&>& inputDataMap
		, std::vector<double>& targetOutputData)= 0;
	virtual void predict(std::unordered_map<std::string, std::vector<double>&>& inputDataMap
		, std::vector<double>& predictionData)= 0;
};


namespace CNTKWrapper
{
	DLL_API IProblem* getProblemInstance(tinyxml2::XMLElement* pNode);
}

#endif