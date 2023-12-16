// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.using System

using System.ComponentModel.DataAnnotations;

namespace LivestreamRecorderService.Models.Options;

public class ConnectionStringOptions
{
    [Required]
    public Uri ServiceEndpoint { get; set; } = new("");
    [Required]
    public string AuthKey { get; set; } = "";

    public void Deconstruct(out Uri serviceEndpoint, out string authKey)
    {
        serviceEndpoint = ServiceEndpoint;
        authKey = AuthKey;
    }
}
