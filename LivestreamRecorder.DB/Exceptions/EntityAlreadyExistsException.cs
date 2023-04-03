// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.using System

using System;

namespace LivestreamRecorder.DB.Exceptions
{
    public class EntityAlreadyExistsException : Exception
    {
        public EntityAlreadyExistsException() { }

        public EntityAlreadyExistsException(string message): base(message) { }
    }
}
