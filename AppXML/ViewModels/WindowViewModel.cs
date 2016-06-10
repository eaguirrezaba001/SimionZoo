﻿using AppXML.Models;
using AppXML.Data;
using System.Collections.ObjectModel;
using System.Xml;
using System.Windows;
using Caliburn.Micro;
using System.Windows.Forms;
using System.IO;
using System.Dynamic;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.IO.Pipes;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Windows.Media;
using System.Threading;
using Herd;
using AppXML.ViewModels;

namespace AppXML.ViewModels
{
    public interface IValidable
    {
        bool validate();
    }
    public interface IGetXml
    {
        List<XmlNode> getXmlNode();
    }
    //[Export(typeof(WindowViewModel))]
    public class WindowViewModel : PropertyChangedBase
    {
        public double ControlHeight
        {
            get
            {
                return (int)System.Windows.SystemParameters.PrimaryScreenHeight;
            }
            set { }
        }
        public double ControlWidth
        {
            get
            {
                return System.Windows.SystemParameters.PrimaryScreenWidth;
            }
            set { }
        }
        //public int MaxHeight
        //{
        //    get
        //    {
        //        return (int)System.Windows.SystemParameters.PrimaryScreenHeight-200;
        //    }
        //    set { }
        //}
        //public double MaxWidth 
        //{
        //    get 
        //    {
        //        return (System.Windows.SystemParameters.PrimaryScreenWidth - 220)/2; 
        //    } 
        //    set { } 
        //}
        private CNode _rootnode;
        private ObservableCollection<BranchViewModel> _branches;
        private XmlDocument _doc;
        //private RightTreeViewModel _graf;
        public ObservableCollection<ValidableAndNodeViewModel> Branch { get { return _branches[0].Class.AllItems; } set { } }
        //public RightTreeViewModel Graf { get { return _graf; }
        //    set 
        //    {
        //        _graf = value; 
        //        NotifyOfPropertyChange(() => Graf); 
        //        NotifyOfPropertyChange(()=> AddChildVisible);
        //        NotifyOfPropertyChange(()=> RemoveChildVisible);

        //    } 
        //}
        private ExperimentQueueViewModel m_experimentQueueViewModel = new ExperimentQueueViewModel();
        public ExperimentQueueViewModel experimentQueueViewModel { get { return m_experimentQueueViewModel; }
            set { m_experimentQueueViewModel = value;
            NotifyOfPropertyChange(() => experimentQueueViewModel);
            }
        }
        private ShepherdViewModel m_shepherdViewModel = new ShepherdViewModel();
        public ShepherdViewModel shepherdViewModel { get { return m_shepherdViewModel; } set { } }

        private bool m_bIsExperimentQueueNotEmpty = false;
        public bool bIsExperimentQueueNotEmpty
        {
            get { return m_bIsExperimentQueueNotEmpty; }
            set { m_bIsExperimentQueueNotEmpty = value;
            NotifyOfPropertyChange(() => bIsExperimentQueueNotEmpty);
            }
        }
        private void checkStackEmpty()
        {
            bool wasEmpty = !m_bIsExperimentQueueNotEmpty;
            if (wasEmpty != m_experimentQueueViewModel.isEmpty())
            {
                m_bIsExperimentQueueNotEmpty = !m_experimentQueueViewModel.isEmpty();
                NotifyOfPropertyChange(() => bIsExperimentQueueNotEmpty);
            }
        }

        private bool m_bIsExperimentRunning = false;
        public bool bIsExperimentRunning
        {
            get { return m_bIsExperimentRunning; }
            set{m_bIsExperimentRunning= value;
            NotifyOfPropertyChange(()=>bIsExperimentRunning);
            NotifyOfPropertyChange(()=>bIsExperimentNotRunning);} }
        public bool bIsExperimentNotRunning
        {
            get { return !m_bIsExperimentRunning; }
            set { }
        }

        private ObservableCollection<string> _apps = new ObservableCollection<string>();
        public ObservableCollection<string> Apps { get { return _apps; } set { } }
        private bool _isSelectedNodeLeaf = false;
        public bool IsSelectedNodeLeafBool { get { return _isSelectedNodeLeaf; } set { _isSelectedNodeLeaf = value; NotifyOfPropertyChange(() => IsSelectedNodeLeaf); } }
        public string IsSelectedNodeLeaf { get { if (_isSelectedNodeLeaf)return "Visible"; else return "Hidden"; } set { } }


        
        private string[] apps;
        private string selectedApp;
        //public SolidColorBrush Color { get { return new SolidColorBrush((Color)ColorConverter.ConvertFromString("White")); } set { } }
 
        public string SelectedApp { get { return selectedApp; } 
            set 
            {
                CNode.cleanAll();
                CApp.cleanApp();

                int index = _apps.IndexOf(value);
                if (index == -1)
                    return;
                selectedApp = value;
                CApp.IsInitializing = true;
                _rootnode = Utility.getRootNode(apps[index]);
                _branches = _rootnode.children;
                _doc = (this._rootnode as CApp).document;
               // _graf = null;
                CApp.IsInitializing = false;
                NotifyOfPropertyChange(() => Branch);
               // NotifyOfPropertyChange(() => Graf);
                NotifyOfPropertyChange(() => rootnode);
             //   NotifyOfPropertyChange(() => RemoveChildVisible);
             //   NotifyOfPropertyChange(() => AddChildVisible);

            } 
        }
       
        public void Change(object sender)
        {
            var x = sender as System.Windows.Controls.TreeView;
            var y = x.SelectedItem;
        }
        public WindowViewModel()
        {
           
             
             //_windowManager = windowManager;
            CApp.IsInitializing = true;
            apps = Directory.GetFiles("..\\config\\apps");
            getAppsNames();
            selectedApp = Apps[0];
            _rootnode = Utility.getRootNode(apps[0]);
            _branches = _rootnode.children;
            _doc = (this._rootnode as CApp).document;
            CApp.IsInitializing = false;
            m_experimentQueueViewModel.setParent(this);
        }
        private void getAppsNames()
        {
            foreach(string app in apps)
            {
                char[] spliter = "\\".ToCharArray();
                string[] tmp = app.Split(spliter);
                tmp = tmp[tmp.Length - 1].Split('.');
                string name =tmp[0];
                _apps.Add(name);
            
            }
        }
        //public void ModifySelectedLeaf()
        //{
        //    if (!validate())
        //        return;
        //    XmlDocument document = new XmlDocument();
        //    XmlNode newRoot = document.ImportNode(_doc.DocumentElement, true);
        //    document.AppendChild(newRoot);
        //    if ( Utility.findDifferences(document, Graf.SelectedTreeNode.Doc)==null)
        //        return;
        //    Graf.SelectedTreeNode.Doc = document;
        //    Graf.SelectedTreeNode.updateDif();
        //    NotifyOfPropertyChange(() => Graf.Tree);
        //    Graf.LoadedAndModified = true;
        //}
        public ObservableCollection<BranchViewModel> Branches { get { return _branches; } set { } }
        public CNode rootnode
        {
            get
            {
                return _rootnode;
            }
            set
            {
                _rootnode = value;
            }

        }

        public void saveExperimentInEditor()
        {
            if (!validate())
                return;
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Experiment | *.experiment";
            sfd.InitialDirectory = "../experiments";
            string CombinedPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "../experiments");
            if (!Directory.Exists(CombinedPath))
                System.IO.Directory.CreateDirectory(CombinedPath);
            sfd.InitialDirectory = System.IO.Path.GetFullPath(CombinedPath); 
            if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _doc.Save(sfd.FileName);
            }



        }
        
        private bool validate()
        {
            _doc.RemoveAll();
            XmlNode rootNode = _doc.CreateElement(_rootnode.name);
            foreach (BranchViewModel branch in _branches)
            {
                if (!branch.validate())
                {
                    DialogViewModel dvm = new DialogViewModel(null, "Error validating de form. Please check form", DialogViewModel.DialogType.Info);
                    dynamic settings = new ExpandoObject();
                    settings.WindowStyle = WindowStyle.ThreeDBorderWindow;
                    settings.ShowInTaskbar = true;
                    settings.Title = "ERROR";

                    new WindowManager().ShowDialog(dvm, null, settings);


                    return false;
                }
                else
                {
                    rootNode.AppendChild(branch.getXmlNode()[0]);
                }
            }
            _doc.AppendChild(rootNode);
            return true;
        }
       
        public void loadExperiment()
        {
            
            string fileDoc = null;
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Experiment | *.experiment";
            ofd.InitialDirectory = Path.Combine(Path.GetDirectoryName(Directory.GetCurrentDirectory()),"experiments");
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                fileDoc = ofd.FileName;
            }
            else
                return;
            
            XmlDocument loadedDocument = new XmlDocument();
            loadedDocument.Load(fileDoc);

            loadExperimentInEditor(loadedDocument);
            //Graf = null;
            
           
           
        }
       
        public void loadExperimentInEditor(XmlDocument experimentXML)
        {
            _doc.RemoveAll();

            //update the app if we need to
            XmlNode experimentNode = experimentXML.FirstChild;
            if (!experimentNode.Name.Equals(selectedApp))
            {
                SelectedApp = experimentNode.Name;
                NotifyOfPropertyChange(() => SelectedApp);

            }
            foreach (BranchViewModel branch in _branches)
            {
                //we have to find the correct data input for every branch we have in the form. If we do not have data we do nothing
                if (experimentNode.HasChildNodes)
                {
                    foreach (XmlNode dataNode in experimentNode.ChildNodes)
                    {
                        if (dataNode.Name == branch.Name)
                        {
                           Utility.fillTheClass(branch.Class, dataNode);
                        }
                    }
                }
            }

        }

        private void setAsNull()
        {
            foreach(BranchViewModel branch in _branches)
            {
                branch.setAsNull();
            }
        }

        public void clearExperimentQueue()
        {
            if (m_experimentQueueViewModel!=null)
            {
                m_experimentQueueViewModel.clear();
                NotifyOfPropertyChange(() => experimentQueueViewModel);
            }
        }
        public void modifySelectedExperiment()
        {
            if (!validate())
                return;
            if (m_experimentQueueViewModel!= null)
            {
                XmlDocument document = new XmlDocument();
                XmlNode newRoot = document.ImportNode(_doc.DocumentElement, true);
                document.AppendChild(newRoot);

                m_experimentQueueViewModel.modifySelectedExperiment(document);
                NotifyOfPropertyChange(() => experimentQueueViewModel);
            }
        }
        public void removeSelectedExperiments()
        {
            if (m_experimentQueueViewModel != null)
            {
                m_experimentQueueViewModel.removeSelectedExperiments();
                NotifyOfPropertyChange(() => experimentQueueViewModel);
                checkStackEmpty();
            }

        }
        public void addExperiment()
        {
            if (!validate())
                return;
            
            XmlDocument document = new XmlDocument();

            XmlNode newRoot = document.ImportNode(_doc.DocumentElement, true);
            document.AppendChild(newRoot);
            //document.Save("copia.tree");
            AppXML.ViewModels.ExperimentViewModel experiment = new AppXML.ViewModels.ExperimentViewModel("Experiment", document);
            m_experimentQueueViewModel.addExperiment(experiment);
            NotifyOfPropertyChange(() => experimentQueueViewModel);
            checkStackEmpty();
        }
        public void runExperiments()
        {
            bool bSuccesfulSave= experimentQueueViewModel.save();


            if (bSuccesfulSave)
            {
                bIsExperimentRunning = true;

                runExperimentQueue();

                bIsExperimentRunning = false;
            }
               
        }

        private CancellationTokenSource m_cancelTokenSource = new CancellationTokenSource();
        
        public void stopExperiments()
        {
            if (m_cancelTokenSource != null)
                m_cancelTokenSource.Cancel();
            experimentQueueViewModel.resetState();
        }

        void runExperimentQueue()
        {
            Task.Factory.StartNew(() =>
                {
                    if (shepherdViewModel.herdAgentList.Count > 0)
                    {
                        //RUN REMOTELY
                        runExperimentQueueRemotely();
                    }
                    else
                    {
                        //RUN LOCALLY
                    }
                });
        }

        

        void runExperimentQueueRemotely()
        {
            int totalCores= 0;
            foreach (HerdAgentViewModel agent in shepherdViewModel.herdAgentList)
                totalCores += agent.numProcessors;

            if (totalCores == 0) return;

            //m_herdAgentNetStreams.Clear();
            int numExperiments= experimentQueueViewModel.experimentQueue.Count;
            double proportion 
                = (double)numExperiments / totalCores;
            int experimentsPerAgent = (int)Math.Ceiling(proportion);
            int numAssignedExperiments = 0;
            
            int offset= 0;
            Dictionary<HerdAgentViewModel, List<ExperimentViewModel>> experimentAssignments
                = new Dictionary<HerdAgentViewModel,List<ExperimentViewModel>>();

            foreach(HerdAgentViewModel agent in shepherdViewModel.herdAgentList)
            {
                if (numAssignedExperiments >= numExperiments) break;

                int amount = (int)Math.Ceiling(agent.numProcessors * proportion);
                amount = Math.Min(experimentQueueViewModel.experimentQueue.Count
                    - numAssignedExperiments, amount);

                List<ExperimentViewModel> assignedExperiments
                    = new List<ExperimentViewModel>();
                for (int i= 0; i<amount; i++)
                    assignedExperiments.Add(experimentQueueViewModel.experimentQueue[offset+i]);
                offset+= amount;

                //assignedExperiments=(experimentQueueViewModel.experimentQueue.Skip(numAssignedExperiments).Take(amount));
                experimentAssignments.Add(agent, assignedExperiments);
                numAssignedExperiments += amount;
            }
            

            ParallelOptions po = new ParallelOptions();
            po.CancellationToken = m_cancelTokenSource.Token;
            po.MaxDegreeOfParallelism = Environment.ProcessorCount - 1;
            try
            {
                Parallel.ForEach(experimentAssignments, po, KeyValuePair =>
                {
                    HerdAgentViewModel herdAgentVM = KeyValuePair.Key;
                    List<ExperimentViewModel> experimentsVM = KeyValuePair.Value;

                    experimentsVM.ForEach((exp) => exp.state = ExperimentViewModel.ExperimentState.WAITING_EXECUTION);
                    CJob job = createJob(experimentQueueViewModel.name, KeyValuePair.Value);

                    Shepherd shepherd = new Shepherd();
                    Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

                    try
                    {
                        shepherd.connectToHerdAgent(KeyValuePair.Key.ipAddress);

                        experimentsVM.ForEach((exp) => exp.state = ExperimentViewModel.ExperimentState.SENDING);
                        herdAgentVM.status = "Sending job query";
                        shepherd.SendJobQuery(job);
                        experimentsVM.ForEach((exp) => exp.state = ExperimentViewModel.ExperimentState.RUNNING);
                        herdAgentVM.status = "Executing job query";

                        string xmlItem;
                        while (true)
                        {
                            shepherd.read();
                            xmlItem = shepherd.m_xmlStream.processNextXMLItem();

                            po.CancellationToken.ThrowIfCancellationRequested();

                            if (xmlItem != "")
                            {
                                XmlDocument doc = new XmlDocument();
                                doc.LoadXml(xmlItem);
                                XmlNode e = doc.DocumentElement;
                                string key = e.Name;
                                XmlNode message = e.FirstChild;
                                ExperimentViewModel experimentVM = experimentsVM.Find(exp => exp.name == key);
                                string content = message.InnerText;
                                if (experimentVM != null)
                                {
                                    if (message.Name == "Progress")
                                    {
                                        double progress = Convert.ToDouble(content);
                                        experimentVM.progress = Convert.ToInt32(progress);
                                    }
                                    else if (message.Name == "Message")
                                    {
                                        experimentVM.addStatusInfoLine(content);
                                    }
                                    else if (message.Name == "End")
                                    {
                                        if (content == "Ok")
                                            experimentVM.state = ExperimentViewModel.ExperimentState.WAITING_RESULT;
                                        else experimentVM.state = ExperimentViewModel.ExperimentState.ERROR;
                                    }
                                }
                                else
                                {
                                    if (key == XMLStream.m_defaultMessageType)
                                    {
                                        if (content == CJobDispatcher.m_endMessage)
                                        {
                                            experimentsVM.ForEach((exp) => exp.state = ExperimentViewModel.ExperimentState.RECEIVING);
                                            herdAgentVM.status = "Receiving output files";
                                            shepherd.ReceiveJobResult();
                                            experimentsVM.ForEach((exp) => exp.state = ExperimentViewModel.ExperimentState.FINISHED);
                                            herdAgentVM.status = "Finished";
                                            break;
                                        }
                                        else if (content == CJobDispatcher.m_errorMessage)
                                        {
                                            herdAgentVM.status = "Error in job";
                                            experimentsVM.ForEach((exp) => exp.state = ExperimentViewModel.ExperimentState.ERROR);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        //quit remote jobs
                        shepherd.writeMessage(Shepherd.m_quitMessage, true);
                        herdAgentVM.status = "";
                    }
                    catch (Exception ex)
                    {
                        //to do: aqui salta cuando hay cualquier problema. Si hay problema hay que volver a lanzarlo
                        //mandar a cualquier maquina que este libre
                        //this.reRun(myPipes.Values);
                        Console.WriteLine(ex.StackTrace);
                    }
                    finally
                    {
                        shepherdViewModel.shepherd.disconnect();
                    }

                });
            }
            catch (OperationCanceledException)
            {
                //because the operation will be rethrown. No need to do anything
            }
            catch (Exception) 
            {
                Console.WriteLine("exceptionally unexpected exception");
            }
        }

        private CJob createJob(string experimentName,List<ExperimentViewModel> experiments)
        {
            CJob job = new CJob();
            job.name = experimentName;
            //prerrequisites
            if (Models.CApp.pre != null)
            {
                foreach (string prerec in Models.CApp.pre)
                    job.inputFiles.Add(prerec);
            }
            //tasks, inputs and outputs
            foreach (ExperimentViewModel experiment in experiments)
            {
                CTask task = new CTask();
                task.name = experiment.name;
                task.exe = Models.CApp.EXE;
                task.arguments = experiment.filePath + " " + experiment.pipeName;
                task.pipe = experiment.pipeName;
                job.tasks.Add(task);
                //add exe file to inputs
                if (!job.inputFiles.Contains(task.exe))
                    job.inputFiles.Add(task.exe);
                //add experiment file to inputs
                if (!job.inputFiles.Contains(experiment.filePath))
                    job.inputFiles.Add(experiment.filePath);
                Utility.getInputsAndOutputs(experiment.filePath, ref job);
            }
            return job;
        }
     
        public void loadExperimentQueue()
        {
            string fileDoc = null;
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Experiment batch | *.exp-batch";
            ofd.InitialDirectory = Path.Combine(Path.GetDirectoryName(Directory.GetCurrentDirectory()), "experiments");
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                fileDoc = ofd.FileName;
            }
            else
                return;
            //this doesn't seem to work
            //Cursor.Current = Cursors.WaitCursor;
            //System.Windows.Forms.Application.DoEvents();

            //LOAD THE EXPERIMENT BATCH IN THE QUEUE
            XmlDocument batchDoc = new XmlDocument();
            batchDoc.Load(fileDoc);
            XmlElement fileRoot = batchDoc.DocumentElement;
            if (fileRoot.Name != "Experiments")
                return;

            foreach (XmlElement element in fileRoot.ChildNodes)
            {
                try
                {
                    string expName = element.Name;
                    string path = element.Attributes["Path"].Value;
                    if (File.Exists(path))
                    {
                        XmlDocument expDocument = new XmlDocument();
                        expDocument.Load(path);
                        m_experimentQueueViewModel.addExperiment(element.Name, expDocument);
                        checkStackEmpty();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.StackTrace);
                }
            }

            m_experimentQueueViewModel.markModified(true);

        }

        public void saveExperimentQueue()
        {
            m_experimentQueueViewModel.save();
         }
       
    }
}
