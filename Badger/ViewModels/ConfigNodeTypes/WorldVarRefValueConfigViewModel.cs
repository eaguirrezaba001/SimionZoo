﻿using System.Xml;
using System.Collections.ObjectModel;
using Badger.Simion;
using System.IO;
using System.Collections.Generic;

namespace Badger.ViewModels
{
    class WorldVarRefValueConfigViewModel: ConfigNodeViewModel
    {
        private ObservableCollection<string> m_variables= new ObservableCollection<string>();
        public ObservableCollection<string> Variables
        {
            get { return m_variables; }
            set { m_variables = value; NotifyOfPropertyChange(() => Variables); }
        }

        public string SelectedVariable
        {
            get { return content; }
            set {
                content = value;
                NotifyOfPropertyChange(() => SelectedVariable);
            }
        }
        
        private WorldVarType m_varType;

        public WorldVarRefValueConfigViewModel(ExperimentViewModel parentExperiment, WorldVarType varType, ConfigNodeViewModel parent, XmlNode definitionNode, string parentXPath, XmlNode configNode = null)
        {
            CommonInitialization(parentExperiment, parent, definitionNode, parentXPath);

            m_varType = varType;

            //the possible values taken by this world variable
            m_variables.Clear();
            foreach (string var in m_parentExperiment.GetWorldVarNameList(m_varType))
                m_variables.Add(var);
            NotifyOfPropertyChange(() => Variables);

            if (configNode != null)
            {
                configNode = configNode[name];
                //is the world variable wired?
                if (configNode.FirstChild != null && configNode.FirstChild.Name == XMLConfig.WireTag)
                {
                    content = configNode.FirstChild.InnerText;
                    m_parentExperiment.AddWire(content);
                }
                else
                    content = configNode.InnerText;
            }

            //if (m_variables.Count==0)
            //{
            //    //Either we have loaded the config but the list is of values has not yet been loaded
            //    //or no config file has been loaded. In Either case, we register for a deferred load step
            //    m_parentExperiment.RegisterDeferredLoadStep(Update);
            //}

            m_parentExperiment.RegisterWorldVarRef(Update);
            m_parentExperiment.RegisterDeferredLoadStep(Update);
        }

        public override ConfigNodeViewModel clone()
        {
            WorldVarRefValueConfigViewModel newInstance=
                new WorldVarRefValueConfigViewModel(m_parentExperiment, m_varType,m_parent, nodeDefinition, m_parent.xPath);
            m_parentExperiment.RegisterWorldVarRef(newInstance.Update);
            newInstance.m_varType = m_varType;
            newInstance.Variables = Variables;
            newInstance.SelectedVariable = SelectedVariable;
            return newInstance;
        }

        public void Update()
        {
            string oldSelectedVariable = SelectedVariable; //need to save it before it is invalidated on updating the list

            m_variables.Clear();
            foreach (string var in m_parentExperiment.GetWorldVarNameList(m_varType))
                m_variables.Add(var);

            NotifyOfPropertyChange(() => Variables);

            SelectedVariable = oldSelectedVariable;
        }

        public override bool Validate()
        {
            return Variables.Contains(content);
        }


        //We need to override this method. If the action/state is wired, the output should include the appropriate tag
        public override void outputXML(StreamWriter writer, SaveMode mode, string leftSpace)
        {
            if (mode == SaveMode.AsExperiment || mode == SaveMode.AsExperimentalUnit)
            {
                List<string> wireNames = new List<string>();
                m_parentExperiment.GetWireNames(ref wireNames);
                if (!wireNames.Contains(content))
                {
                    //unwired
                    writer.Write(leftSpace + "<" + name + ">" + content + "</" + name + ">\n");
                }
                else
                {
                    //wired
                    writer.Write(leftSpace + "<" + name + "><" + XMLConfig.WireTag + ">"
                        + content + "</" + XMLConfig.WireTag + "></" + name + ">\n");
                }
            }
        }
    }
}
