// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

global using System;
global using System.Threading;
global using System.Threading.Tasks;
global using Belay.Core;
// Removed namespaces after architectural simplification:
// - Belay.Core.Caching (replaced by SimpleCache)  
// - Belay.Core.Execution (replaced by DirectExecutor)
// - Belay.Core.Communication (replaced by DeviceConnection)
global using FluentAssertions;
global using Microsoft.Extensions.Logging;
global using Moq;
global using Xunit;
