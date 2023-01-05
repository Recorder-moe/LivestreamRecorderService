// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.using System

namespace LivestreamRecorderService.Models.Options;

public class ConnectionStringOptions
{
    public required Uri ServiceEndpoint { get; set; }
    public required string AuthKey { get; set; }

    public void Deconstruct(out Uri serviceEndpoint, out string authKey)
    {
        serviceEndpoint = ServiceEndpoint;
        authKey = AuthKey;
    }
}
