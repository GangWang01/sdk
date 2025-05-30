﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Utilities;
using Microsoft.NET.Sdk.Publish.Tasks.MsDeploy;
using Microsoft.NET.Sdk.Publish.Tasks.Properties;
using Microsoft.NET.Sdk.Publish.Tasks.Xdt;
using Microsoft.Web.XmlTransform;
using Framework = Microsoft.Build.Framework;
using Xml = System.Xml;

namespace Microsoft.NET.Sdk.Publish.Tasks.Tasks.MsDeploy
{
    public class ImportParameterFile : Task
    {
        private Framework.ITaskItem[]? m_sourceFiles = null;
        private List<Framework.ITaskItem> m_parametersList = new(8);

        [Framework.Required]
        public Framework.ITaskItem[]? Files
        {
            get { return m_sourceFiles; }
            set { m_sourceFiles = value; }
        }

        [Framework.Output]
        public Framework.ITaskItem[] Result
        {
            get { return m_parametersList.ToArray(); }
        }

        public bool DisableEscapeMSBuildVariable
        {
            get;
            set;
        }

        /// <summary>
        /// Utility function to pare the top level of the element
        /// </summary>
        /// <param name="element"></param>
        private void ReadParametersElement(Xml.XmlElement element)
        {
            Debug.Assert(element != null);
            if (element is not null && string.Compare(element.Name, "parameters", StringComparison.OrdinalIgnoreCase) == 0)
            {
                foreach (Xml.XmlNode childNode in element.ChildNodes)
                {
                    Xml.XmlElement? childElement = childNode as Xml.XmlElement;
                    if (childElement != null)
                    {
                        ReadParameterElement(childElement);
                    }
                }
            }
        }

        /// <summary>
        /// Parse the Parameter element
        /// </summary>
        /// <param name="element"></param>
        private void ReadParameterElement(Xml.XmlElement element)
        {
            Debug.Assert(element != null);
            if (element is not null && string.Compare(element.Name, "parameter", StringComparison.OrdinalIgnoreCase) == 0)
            {
                Xml.XmlAttribute? nameAttribute = element.Attributes.GetNamedItem("name") as Xml.XmlAttribute;
                if (nameAttribute != null)
                {
                    TaskItem taskItem = new(nameAttribute.Value);
                    foreach (Xml.XmlNode attribute in element.Attributes)
                    {
                        string attributeName = attribute.Name.ToLower(System.Globalization.CultureInfo.InvariantCulture);
                        if (string.CompareOrdinal(attributeName, "xmlns") == 0
                            || attribute.Name.StartsWith("xmlns:", StringComparison.Ordinal)
                            || string.CompareOrdinal(attributeName, "name") == 0
                            )
                        {
                            continue;
                        }
                        string? value = DisableEscapeMSBuildVariable ? attribute.Value : Utility.EscapeTextForMSBuildVariable(attribute.Value);
                        taskItem.SetMetadata(attribute.Name, value);
                    }
                    // work around the MSDeploy.exe limitation of the Parameter must have the ParameterEntry.
                    // m_parametersList.Add(taskItem);
                    bool fAddNoParameterEntryParameter = true;

                    foreach (Xml.XmlNode childNode in element.ChildNodes)
                    {
                        Xml.XmlElement? childElement = childNode as Xml.XmlElement;
                        if (childElement != null)
                        {
                            TaskItem? childEntry = ReadParameterEntryElement(childElement, taskItem);
                            if (childEntry != null)
                            {
                                fAddNoParameterEntryParameter = false; // we have Parameter entry, suppress adding the Parameter with no entry
                                m_parametersList.Add(childEntry);
                            }
                        }
                    }

                    if (fAddNoParameterEntryParameter)
                    {
                        // finally add a parameter without any entry
                        m_parametersList.Add(taskItem);
                    }
                }
            }
        }

        /// <summary>
        /// Helper function to parse the ParameterEntryElement
        /// </summary>
        /// <param name="element"></param>
        /// <param name="parentItem"></param>
        /// <returns></returns>
        private TaskItem? ReadParameterEntryElement(Xml.XmlElement element, TaskItem parentItem)
        {
            Debug.Assert(element != null && parentItem != null);
            TaskItem? taskItem = null;
            if (element is not null && string.Compare(element.Name, "parameterEntry", StringComparison.OrdinalIgnoreCase) == 0)
            {
                taskItem = new TaskItem(parentItem);
                taskItem.RemoveMetadata("OriginalItemSpec");
                foreach (Xml.XmlNode attribute in element.Attributes)
                {
                    if (attribute != null && attribute.Name != null && attribute.Value != null)
                    {
                        string? value = DisableEscapeMSBuildVariable ? attribute.Value : Utility.EscapeTextForMSBuildVariable(attribute.Value);
                        taskItem.SetMetadata(attribute.Name, value);
                    }
                }
            }
            else if (element is not null && string.Compare(element.Name, "parameterValidation", StringComparison.OrdinalIgnoreCase) == 0)
            {
                taskItem = new TaskItem(parentItem);
                taskItem.RemoveMetadata("OriginalItemSpec");
                taskItem.SetMetadata("Element", "parameterValidation");
                foreach (Xml.XmlNode attribute in element.Attributes)
                {
                    if (attribute != null && attribute.Name != null && attribute.Value != null)
                    {
                        string? value = DisableEscapeMSBuildVariable ? attribute.Value : Utility.EscapeTextForMSBuildVariable(attribute.Value);
                        taskItem.SetMetadata(attribute.Name, value);
                    }
                }
            }
            return taskItem;
        }

        /// <summary>
        /// The task execute function
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            bool succeeded = true;
            IXmlTransformationLogger logger = new TaskTransformationLogger(Log);

            if (m_sourceFiles != null)
            {
                try
                {
                    foreach (Framework.ITaskItem item in m_sourceFiles)
                    {
                        string filePath = item.GetMetadata("FullPath");
                        if (!File.Exists(filePath))
                        {
                            Log.LogError(Resources.BUILDTASK_TransformXml_SourceLoadFailed, new object[] { filePath });
                            succeeded = false;
                            break;
                        }
                        Xml.XmlDocument document = new();
                        document.Load(filePath);
                        foreach (Xml.XmlNode node in document.ChildNodes)
                        {
                            Xml.XmlElement? element = node as Xml.XmlElement;
                            if (element != null)
                            {
                                ReadParametersElement(element);
                            }
                        }
                    }
                }
                catch (Xml.XmlException ex)
                {
                    if (ex.SourceUri is not null)
                    {
                        Uri sourceUri = new(ex.SourceUri);
                        logger.LogError(sourceUri.LocalPath, ex.LineNumber, ex.LinePosition, ex.Message);
                    }
                    succeeded = false;
                }
                catch (Exception ex)
                {
                    logger.LogErrorFromException(ex);
                    succeeded = false;
                }
            }
            return succeeded;
        }
    }
}
