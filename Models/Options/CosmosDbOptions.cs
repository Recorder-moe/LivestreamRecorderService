// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.using System

using System.Collections.Generic;

namespace LivestreamRecorderService.Models.Options;

public class CosmosDbOptions
{
#pragma warning disable IDE1006 // 命名樣式
public const string ConfigurationSectionName = "CosmosDb";
#pragma warning restore IDE1006 // 命名樣式

    public required string DatabaseName { get; set; }
    public required List<CollectionInfo> CollectionNames { get; set; }

    public void Deconstruct(out string databaseName, out List<CollectionInfo> collectionNames)
    {
        databaseName = DatabaseName;
        collectionNames = CollectionNames;
    }
}

public class CollectionInfo
{
    public required string Name { get; set; }
    public string? PartitionKey { get; set; }
}
