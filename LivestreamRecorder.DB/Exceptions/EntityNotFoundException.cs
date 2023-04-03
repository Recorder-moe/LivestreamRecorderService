// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.using System

using System;

namespace LivestreamRecorder.DB.Exceptions
{
    public class EntityNotFoundException : Exception
    {
        public EntityNotFoundException() { }

        public EntityNotFoundException(string message): base(message) { }
    }
}
