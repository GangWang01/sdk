﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Newtonsoft.Json;

namespace Microsoft.CodeAnalysis.Razor.Serialization;

internal class TagHelperDescriptorJsonConverter : JsonConverter
{
    public static readonly TagHelperDescriptorJsonConverter Instance = new();

    public override bool CanConvert(Type objectType)
    {
        return typeof(TagHelperDescriptor).IsAssignableFrom(objectType);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType != JsonToken.StartObject)
        {
            return null;
        }

        // Required tokens (order matters)
        var descriptorKind = reader.ReadNextStringProperty(nameof(TagHelperDescriptor.Kind));
        var typeName = reader.ReadNextStringProperty(nameof(TagHelperDescriptor.Name));
        var assemblyName = reader.ReadNextStringProperty(nameof(TagHelperDescriptor.AssemblyName));
        using var _ = TagHelperDescriptorBuilder.GetPooledInstance(descriptorKind, typeName, assemblyName, out var builder);

        reader.ReadProperties(propertyName =>
        {
            switch (propertyName)
            {
                case nameof(TagHelperDescriptor.Documentation):
                    if (reader.Read())
                    {
                        var documentation = (string)reader.Value;
                        builder.SetDocumentation(documentation);
                    }
                    break;
                case nameof(TagHelperDescriptor.TagOutputHint):
                    if (reader.Read())
                    {
                        var tagOutputHint = (string)reader.Value;
                        builder.TagOutputHint = tagOutputHint;
                    }
                    break;
                case nameof(TagHelperDescriptor.CaseSensitive):
                    if (reader.Read())
                    {
                        var caseSensitive = (bool)reader.Value;
                        builder.CaseSensitive = caseSensitive;
                    }
                    break;
                case nameof(TagHelperDescriptor.TagMatchingRules):
                    ReadTagMatchingRules(reader, builder);
                    break;
                case nameof(TagHelperDescriptor.BoundAttributes):
                    ReadBoundAttributes(reader, builder);
                    break;
                case nameof(TagHelperDescriptor.AllowedChildTags):
                    ReadAllowedChildTags(reader, builder);
                    break;
                case nameof(TagHelperDescriptor.Diagnostics):
                    ReadDiagnostics(reader, builder.Diagnostics);
                    break;
                case nameof(TagHelperDescriptor.Metadata):
                    ReadMetadata(reader, builder.Metadata);
                    break;
            }
        });

        return builder.Build();
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var tagHelper = (TagHelperDescriptor)value;

        writer.WriteStartObject();

        writer.WritePropertyName(nameof(TagHelperDescriptor.Kind));
        writer.WriteValue(tagHelper.Kind);

        writer.WritePropertyName(nameof(TagHelperDescriptor.Name));
        writer.WriteValue(tagHelper.Name);

        writer.WritePropertyName(nameof(TagHelperDescriptor.AssemblyName));
        writer.WriteValue(tagHelper.AssemblyName);

        if (tagHelper.Documentation != null)
        {
            writer.WritePropertyName(nameof(TagHelperDescriptor.Documentation));
            writer.WriteValue(tagHelper.Documentation);
        }

        if (tagHelper.TagOutputHint != null)
        {
            writer.WritePropertyName(nameof(TagHelperDescriptor.TagOutputHint));
            writer.WriteValue(tagHelper.TagOutputHint);
        }

        writer.WritePropertyName(nameof(TagHelperDescriptor.CaseSensitive));
        writer.WriteValue(tagHelper.CaseSensitive);

        writer.WritePropertyName(nameof(TagHelperDescriptor.TagMatchingRules));
        writer.WriteStartArray();
        foreach (var ruleDescriptor in tagHelper.TagMatchingRules)
        {
            WriteTagMatchingRule(writer, ruleDescriptor, serializer);
        }
        writer.WriteEndArray();

        if (tagHelper.BoundAttributes != null && tagHelper.BoundAttributes.Length > 0)
        {
            writer.WritePropertyName(nameof(TagHelperDescriptor.BoundAttributes));
            writer.WriteStartArray();
            foreach (var boundAttribute in tagHelper.BoundAttributes)
            {
                WriteBoundAttribute(writer, boundAttribute, serializer);
            }
            writer.WriteEndArray();
        }

        if (tagHelper.AllowedChildTags != null && tagHelper.AllowedChildTags.Length > 0)
        {
            writer.WritePropertyName(nameof(TagHelperDescriptor.AllowedChildTags));
            writer.WriteStartArray();
            foreach (var allowedChildTag in tagHelper.AllowedChildTags)
            {
                WriteAllowedChildTags(writer, allowedChildTag, serializer);
            }
            writer.WriteEndArray();
        }

        if (tagHelper.Diagnostics != null && tagHelper.Diagnostics.Length > 0)
        {
            writer.WritePropertyName(nameof(TagHelperDescriptor.Diagnostics));
            serializer.Serialize(writer, tagHelper.Diagnostics);
        }

        writer.WritePropertyName(nameof(TagHelperDescriptor.Metadata));
        WriteMetadata(writer, tagHelper.Metadata);

        writer.WriteEndObject();
    }

    private static void WriteAllowedChildTags(JsonWriter writer, AllowedChildTagDescriptor allowedChildTag, JsonSerializer serializer)
    {
        writer.WriteStartObject();

        writer.WritePropertyName(nameof(AllowedChildTagDescriptor.Name));
        writer.WriteValue(allowedChildTag.Name);

        writer.WritePropertyName(nameof(AllowedChildTagDescriptor.DisplayName));
        writer.WriteValue(allowedChildTag.DisplayName);

        writer.WritePropertyName(nameof(AllowedChildTagDescriptor.Diagnostics));
        serializer.Serialize(writer, allowedChildTag.Diagnostics);

        writer.WriteEndObject();
    }

    private static void WriteBoundAttribute(JsonWriter writer, BoundAttributeDescriptor boundAttribute, JsonSerializer serializer)
    {
        writer.WriteStartObject();

        writer.WritePropertyName(nameof(BoundAttributeDescriptor.Name));
        writer.WriteValue(boundAttribute.Name);

        writer.WritePropertyName(nameof(BoundAttributeDescriptor.TypeName));
        writer.WriteValue(boundAttribute.TypeName);

        if (boundAttribute.IsEnum)
        {
            writer.WritePropertyName(nameof(BoundAttributeDescriptor.IsEnum));
            writer.WriteValue(boundAttribute.IsEnum);
        }

        if (boundAttribute.IndexerNamePrefix != null)
        {
            writer.WritePropertyName(nameof(BoundAttributeDescriptor.IndexerNamePrefix));
            writer.WriteValue(boundAttribute.IndexerNamePrefix);
        }

        if (boundAttribute.IsEditorRequired)
        {
            writer.WritePropertyName(nameof(BoundAttributeDescriptor.IsEditorRequired));
            writer.WriteValue(boundAttribute.IsEditorRequired);
        }

        if (boundAttribute.IndexerTypeName != null)
        {
            writer.WritePropertyName(nameof(BoundAttributeDescriptor.IndexerTypeName));
            writer.WriteValue(boundAttribute.IndexerTypeName);
        }

        if (boundAttribute.Documentation != null)
        {
            writer.WritePropertyName(nameof(BoundAttributeDescriptor.Documentation));
            writer.WriteValue(boundAttribute.Documentation);
        }

        if (boundAttribute.Diagnostics != null && boundAttribute.Diagnostics.Length > 0)
        {
            writer.WritePropertyName(nameof(BoundAttributeDescriptor.Diagnostics));
            serializer.Serialize(writer, boundAttribute.Diagnostics);
        }

        writer.WritePropertyName(nameof(BoundAttributeDescriptor.Metadata));
        WriteMetadata(writer, boundAttribute.Metadata);

        if (boundAttribute.Parameters != null && boundAttribute.Parameters.Length > 0)
        {
            writer.WritePropertyName(nameof(BoundAttributeDescriptor.Parameters));
            writer.WriteStartArray();
            foreach (var boundAttributeParameter in boundAttribute.Parameters)
            {
                WriteBoundAttributeParameter(writer, boundAttributeParameter, serializer);
            }
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static void WriteBoundAttributeParameter(JsonWriter writer, BoundAttributeParameterDescriptor boundAttributeParameter, JsonSerializer serializer)
    {
        writer.WriteStartObject();

        writer.WritePropertyName(nameof(BoundAttributeParameterDescriptor.Name));
        writer.WriteValue(boundAttributeParameter.Name);

        writer.WritePropertyName(nameof(BoundAttributeParameterDescriptor.PropertyName));
        writer.WriteValue(boundAttributeParameter.PropertyName);

        writer.WritePropertyName(nameof(BoundAttributeParameterDescriptor.TypeName));
        writer.WriteValue(boundAttributeParameter.TypeName);

        if (boundAttributeParameter.IsEnum)
        {
            writer.WritePropertyName(nameof(BoundAttributeParameterDescriptor.IsEnum));
            writer.WriteValue(boundAttributeParameter.IsEnum);
        }

        if (boundAttributeParameter.Documentation != null)
        {
            writer.WritePropertyName(nameof(BoundAttributeParameterDescriptor.Documentation));
            writer.WriteValue(boundAttributeParameter.Documentation);
        }

        if (boundAttributeParameter.Diagnostics != null && boundAttributeParameter.Diagnostics.Length > 0)
        {
            writer.WritePropertyName(nameof(BoundAttributeParameterDescriptor.Diagnostics));
            serializer.Serialize(writer, boundAttributeParameter.Diagnostics);
        }

        writer.WriteEndObject();
    }

    private static void WriteMetadata(JsonWriter writer, MetadataCollection metadata)
    {
        writer.WriteStartObject();
        foreach (var kvp in metadata)
        {
            writer.WritePropertyName(kvp.Key);
            writer.WriteValue(kvp.Value);
        }
        writer.WriteEndObject();
    }

    private static void WriteTagMatchingRule(JsonWriter writer, TagMatchingRuleDescriptor ruleDescriptor, JsonSerializer serializer)
    {
        writer.WriteStartObject();

        writer.WritePropertyName(nameof(TagMatchingRuleDescriptor.TagName));
        writer.WriteValue(ruleDescriptor.TagName);

        if (ruleDescriptor.ParentTag != null)
        {
            writer.WritePropertyName(nameof(TagMatchingRuleDescriptor.ParentTag));
            writer.WriteValue(ruleDescriptor.ParentTag);
        }

        if (ruleDescriptor.TagStructure != default)
        {
            writer.WritePropertyName(nameof(TagMatchingRuleDescriptor.TagStructure));
            writer.WriteValue(ruleDescriptor.TagStructure);
        }

        if (ruleDescriptor.Attributes != null && ruleDescriptor.Attributes.Length > 0)
        {
            writer.WritePropertyName(nameof(TagMatchingRuleDescriptor.Attributes));
            writer.WriteStartArray();
            foreach (var requiredAttribute in ruleDescriptor.Attributes)
            {
                WriteRequiredAttribute(writer, requiredAttribute, serializer);
            }
            writer.WriteEndArray();
        }

        if (ruleDescriptor.Diagnostics != null && ruleDescriptor.Diagnostics.Length > 0)
        {
            writer.WritePropertyName(nameof(TagMatchingRuleDescriptor.Diagnostics));
            serializer.Serialize(writer, ruleDescriptor.Diagnostics);
        }

        writer.WriteEndObject();
    }

    private static void WriteRequiredAttribute(JsonWriter writer, RequiredAttributeDescriptor requiredAttribute, JsonSerializer serializer)
    {
        writer.WriteStartObject();

        writer.WritePropertyName(nameof(RequiredAttributeDescriptor.Name));
        writer.WriteValue(requiredAttribute.Name);

        if (requiredAttribute.NameComparison != default)
        {
            writer.WritePropertyName(nameof(RequiredAttributeDescriptor.NameComparison));
            writer.WriteValue(requiredAttribute.NameComparison);
        }

        if (requiredAttribute.Value != null)
        {
            writer.WritePropertyName(nameof(RequiredAttributeDescriptor.Value));
            writer.WriteValue(requiredAttribute.Value);
        }

        if (requiredAttribute.ValueComparison != default)
        {
            writer.WritePropertyName(nameof(RequiredAttributeDescriptor.ValueComparison));
            writer.WriteValue(requiredAttribute.ValueComparison);
        }

        if (requiredAttribute.Diagnostics != null && requiredAttribute.Diagnostics.Length > 0)
        {
            writer.WritePropertyName(nameof(RequiredAttributeDescriptor.Diagnostics));
            serializer.Serialize(writer, requiredAttribute.Diagnostics);
        }

        writer.WriteEndObject();
    }

    private static void ReadBoundAttributes(JsonReader reader, TagHelperDescriptorBuilder builder)
    {
        if (!reader.Read())
        {
            return;
        }

        if (reader.TokenType != JsonToken.StartArray)
        {
            return;
        }

        do
        {
            ReadBoundAttribute(reader, builder);
        } while (reader.TokenType != JsonToken.EndArray);
    }

    private static void ReadBoundAttribute(JsonReader reader, TagHelperDescriptorBuilder builder)
    {
        if (!reader.Read())
        {
            return;
        }

        if (reader.TokenType != JsonToken.StartObject)
        {
            return;
        }

        builder.BindAttribute(attribute =>
        {
            reader.ReadProperties(propertyName =>
            {
                switch (propertyName)
                {
                    case nameof(BoundAttributeDescriptor.Name):
                        if (reader.Read())
                        {
                            var name = (string)reader.Value;
                            attribute.Name = name;
                        }
                        break;
                    case nameof(BoundAttributeDescriptor.TypeName):
                        if (reader.Read())
                        {
                            var typeName = (string)reader.Value;
                            attribute.TypeName = typeName;
                        }
                        break;
                    case nameof(BoundAttributeDescriptor.Documentation):
                        if (reader.Read())
                        {
                            var documentation = (string)reader.Value;
                            attribute.SetDocumentation(documentation);
                        }
                        break;
                    case nameof(BoundAttributeDescriptor.IndexerNamePrefix):
                        if (reader.Read())
                        {
                            var indexerNamePrefix = (string)reader.Value;
                            if (indexerNamePrefix != null)
                            {
                                attribute.IsDictionary = true;
                                attribute.IndexerAttributeNamePrefix = indexerNamePrefix;
                            }
                        }
                        break;
                    case nameof(BoundAttributeDescriptor.IndexerTypeName):
                        if (reader.Read())
                        {
                            var indexerTypeName = (string)reader.Value;
                            if (indexerTypeName != null)
                            {
                                attribute.IsDictionary = true;
                                attribute.IndexerValueTypeName = indexerTypeName;
                            }
                        }
                        break;
                    case nameof(BoundAttributeDescriptor.IsEnum):
                        if (reader.Read())
                        {
                            var isEnum = (bool)reader.Value;
                            attribute.IsEnum = isEnum;
                        }
                        break;
                    case nameof(BoundAttributeDescriptor.IsEditorRequired):
                        if (reader.Read())
                        {
                            var value = (bool)reader.Value;
                            attribute.IsEditorRequired = value;
                        }
                        break;
                    case nameof(BoundAttributeDescriptor.Parameters):
                        ReadBoundAttributeParameters(reader, attribute);
                        break;
                    case nameof(BoundAttributeDescriptor.Diagnostics):
                        ReadDiagnostics(reader, attribute.Diagnostics);
                        break;
                    case nameof(BoundAttributeDescriptor.Metadata):
                        ReadMetadata(reader, attribute.Metadata);
                        break;
                }
            });
        });
    }

    private static void ReadBoundAttributeParameters(JsonReader reader, BoundAttributeDescriptorBuilder builder)
    {
        if (!reader.Read())
        {
            return;
        }

        if (reader.TokenType != JsonToken.StartArray)
        {
            return;
        }

        do
        {
            ReadBoundAttributeParameter(reader, builder);
        } while (reader.TokenType != JsonToken.EndArray);
    }

    private static void ReadBoundAttributeParameter(JsonReader reader, BoundAttributeDescriptorBuilder builder)
    {
        if (!reader.Read())
        {
            return;
        }

        if (reader.TokenType != JsonToken.StartObject)
        {
            return;
        }

        builder.BindAttributeParameter(parameter =>
        {
            reader.ReadProperties(jsonPropertyName =>
            {
                switch (jsonPropertyName)
                {
                    case nameof(BoundAttributeParameterDescriptor.Name):
                        if (reader.Read())
                        {
                            var name = (string)reader.Value;
                            parameter.Name = name;
                        }
                        break;
                    case nameof(BoundAttributeParameterDescriptor.PropertyName):
                        if (reader.Read())
                        {
                            var propertyName = (string)reader.Value;
                            parameter.PropertyName = propertyName;
                        }
                        break;
                    case nameof(BoundAttributeParameterDescriptor.TypeName):
                        if (reader.Read())
                        {
                            var typeName = (string)reader.Value;
                            parameter.TypeName = typeName;
                        }
                        break;
                    case nameof(BoundAttributeParameterDescriptor.IsEnum):
                        if (reader.Read())
                        {
                            var isEnum = (bool)reader.Value;
                            parameter.IsEnum = isEnum;
                        }
                        break;
                    case nameof(BoundAttributeParameterDescriptor.Documentation):
                        if (reader.Read())
                        {
                            var documentation = (string)reader.Value;
                            parameter.SetDocumentation(documentation);
                        }
                        break;
                    case nameof(BoundAttributeParameterDescriptor.Diagnostics):
                        ReadDiagnostics(reader, parameter.Diagnostics);
                        break;
                }
            });
        });
    }

    private static void ReadTagMatchingRules(JsonReader reader, TagHelperDescriptorBuilder builder)
    {
        if (!reader.Read())
        {
            return;
        }

        if (reader.TokenType != JsonToken.StartArray)
        {
            return;
        }

        do
        {
            ReadTagMatchingRule(reader, builder);
        } while (reader.TokenType != JsonToken.EndArray);
    }

    private static void ReadTagMatchingRule(JsonReader reader, TagHelperDescriptorBuilder builder)
    {
        if (!reader.Read())
        {
            return;
        }

        if (reader.TokenType != JsonToken.StartObject)
        {
            return;
        }

        builder.TagMatchingRule(rule =>
        {
            reader.ReadProperties(propertyName =>
            {
                switch (propertyName)
                {
                    case nameof(TagMatchingRuleDescriptor.TagName):
                        if (reader.Read())
                        {
                            var tagName = (string)reader.Value;
                            rule.TagName = tagName;
                        }
                        break;
                    case nameof(TagMatchingRuleDescriptor.ParentTag):
                        if (reader.Read())
                        {
                            var parentTag = (string)reader.Value;
                            rule.ParentTag = parentTag;
                        }
                        break;
                    case nameof(TagMatchingRuleDescriptor.TagStructure):
                        rule.TagStructure = (TagStructure)reader.ReadAsInt32();
                        break;
                    case nameof(TagMatchingRuleDescriptor.Attributes):
                        ReadRequiredAttributeValues(reader, rule);
                        break;
                    case nameof(TagMatchingRuleDescriptor.Diagnostics):
                        ReadDiagnostics(reader, rule.Diagnostics);
                        break;
                }
            });
        });
    }

    private static void ReadRequiredAttributeValues(JsonReader reader, TagMatchingRuleDescriptorBuilder builder)
    {
        if (!reader.Read())
        {
            return;
        }

        if (reader.TokenType != JsonToken.StartArray)
        {
            return;
        }

        do
        {
            ReadRequiredAttribute(reader, builder);
        } while (reader.TokenType != JsonToken.EndArray);
    }

    private static void ReadRequiredAttribute(JsonReader reader, TagMatchingRuleDescriptorBuilder builder)
    {
        if (!reader.Read())
        {
            return;
        }

        if (reader.TokenType != JsonToken.StartObject)
        {
            return;
        }

        builder.Attribute(attribute =>
        {
            reader.ReadProperties(propertyName =>
            {
                switch (propertyName)
                {
                    case nameof(RequiredAttributeDescriptor.Name):
                        if (reader.Read())
                        {
                            var name = (string)reader.Value;
                            attribute.Name = name;
                        }
                        break;
                    case nameof(RequiredAttributeDescriptor.NameComparison):
                        var nameComparison = (RequiredAttributeNameComparison)reader.ReadAsInt32();
                        attribute.NameComparison = nameComparison;
                        break;
                    case nameof(RequiredAttributeDescriptor.Value):
                        if (reader.Read())
                        {
                            var value = (string)reader.Value;
                            attribute.Value = value;
                        }
                        break;
                    case nameof(RequiredAttributeDescriptor.ValueComparison):
                        var valueComparison = (RequiredAttributeValueComparison)reader.ReadAsInt32();
                        attribute.ValueComparison = valueComparison;
                        break;
                    case nameof(RequiredAttributeDescriptor.Diagnostics):
                        ReadDiagnostics(reader, attribute.Diagnostics);
                        break;
                }
            });
        });
    }

    private static void ReadAllowedChildTags(JsonReader reader, TagHelperDescriptorBuilder builder)
    {
        if (!reader.Read())
        {
            return;
        }

        if (reader.TokenType != JsonToken.StartArray)
        {
            return;
        }

        do
        {
            ReadAllowedChildTag(reader, builder);
        } while (reader.TokenType != JsonToken.EndArray);
    }

    private static void ReadAllowedChildTag(JsonReader reader, TagHelperDescriptorBuilder builder)
    {
        if (!reader.Read())
        {
            return;
        }

        if (reader.TokenType != JsonToken.StartObject)
        {
            return;
        }

        builder.AllowChildTag(childTag =>
        {
            reader.ReadProperties(propertyName =>
            {
                switch (propertyName)
                {
                    case nameof(AllowedChildTagDescriptor.Name):
                        if (reader.Read())
                        {
                            var name = (string)reader.Value;
                            childTag.Name = name;
                        }
                        break;
                    case nameof(AllowedChildTagDescriptor.DisplayName):
                        if (reader.Read())
                        {
                            var displayName = (string)reader.Value;
                            childTag.DisplayName = displayName;
                        }
                        break;
                    case nameof(AllowedChildTagDescriptor.Diagnostics):
                        ReadDiagnostics(reader, childTag.Diagnostics);
                        break;
                }
            });
        });
    }

    private static void ReadMetadata(JsonReader reader, IDictionary<string, string> metadata)
    {
        if (!reader.Read())
        {
            return;
        }

        if (reader.TokenType != JsonToken.StartObject)
        {
            return;
        }

        reader.ReadProperties(propertyName =>
        {
            if (reader.Read())
            {
                var value = (string)reader.Value;
                metadata[propertyName] = value;
            }
        });
    }

    private static void ReadDiagnostics(JsonReader reader, IList<RazorDiagnostic> diagnostics)
    {
        if (!reader.Read())
        {
            return;
        }

        if (reader.TokenType != JsonToken.StartArray)
        {
            return;
        }

        do
        {
            ReadDiagnostic(reader, diagnostics);
        }
        while (reader.TokenType != JsonToken.EndArray);
    }

    private static void ReadDiagnostic(JsonReader reader, IList<RazorDiagnostic> diagnostics)
    {
        if (!reader.Read())
        {
            return;
        }

        if (reader.TokenType != JsonToken.StartObject)
        {
            return;
        }

        string id = default;
        int severity = default;
        string message = default;
        SourceSpan sourceSpan = default;

        reader.ReadProperties(propertyName =>
        {
            switch (propertyName)
            {
                case nameof(RazorDiagnostic.Id):
                    if (reader.Read())
                    {
                        id = (string)reader.Value;
                    }
                    break;
                case nameof(RazorDiagnostic.Severity):
                    severity = reader.ReadAsInt32().Value;
                    break;
                case "Message":
                    if (reader.Read())
                    {
                        message = (string)reader.Value;
                    }
                    break;
                case nameof(RazorDiagnostic.Span):
                    sourceSpan = ReadSourceSpan(reader);
                    break;
            }
        });

        var descriptor = new RazorDiagnosticDescriptor(id, message, (RazorDiagnosticSeverity)severity);

        var diagnostic = RazorDiagnostic.Create(descriptor, sourceSpan);
        diagnostics.Add(diagnostic);
    }

    private static SourceSpan ReadSourceSpan(JsonReader reader)
    {
        if (!reader.Read())
        {
            return SourceSpan.Undefined;
        }

        if (reader.TokenType != JsonToken.StartObject)
        {
            return SourceSpan.Undefined;
        }

        string filePath = default;
        int absoluteIndex = default;
        int lineIndex = default;
        int characterIndex = default;
        int length = default;

        reader.ReadProperties(propertyName =>
        {
            switch (propertyName)
            {
                case nameof(SourceSpan.FilePath):
                    if (reader.Read())
                    {
                        filePath = (string)reader.Value;
                    }
                    break;
                case nameof(SourceSpan.AbsoluteIndex):
                    absoluteIndex = reader.ReadAsInt32().Value;
                    break;
                case nameof(SourceSpan.LineIndex):
                    lineIndex = reader.ReadAsInt32().Value;
                    break;
                case nameof(SourceSpan.CharacterIndex):
                    characterIndex = reader.ReadAsInt32().Value;
                    break;
                case nameof(SourceSpan.Length):
                    length = reader.ReadAsInt32().Value;
                    break;
            }
        });

        var sourceSpan = new SourceSpan(filePath, absoluteIndex, lineIndex, characterIndex, length);
        return sourceSpan;
    }
}
