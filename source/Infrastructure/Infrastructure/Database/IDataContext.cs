﻿// ==============================================================================================================
// Microsoft patterns & practices
// CQRS Journey project
// ==============================================================================================================
// ©2012 Microsoft. All rights reserved. Certain content used with permission from contributors
// http://go.microsoft.com/fwlink/p/?LinkID=258575
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance 
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is 
// distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and limitations under the License.
// ==============================================================================================================

namespace Infrastructure.Database;

using System;

/// <summary>
/// Represents a temporary data context to load and save an entity that implements <see cref="IAggregateRoot"/>.
/// </summary>
/// <typeparam name="T">The entity type that will be retrieved or persisted.</typeparam>
public interface IDataContext<T> : IDisposable
    where T : IAggregateRoot
{
    T Find(Guid id);

    void Save(T aggregate);
}

