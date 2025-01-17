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

using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Sql.MessageLog
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Infrastructure.MessageLog;
    using Infrastructure.Messaging;
    using Infrastructure.Serialization;

    public static class IDictionaryExtensions
    {
        public static TV GetValue<TK, TV>(this IDictionary<TK, TV> dict, TK key, TV defaultValue = default(TV))
        {
            return dict.TryGetValue(key, out var value) ? value : defaultValue;
        }
    }
    
    public class SqlMessageLog : IEventLogReader
    {
        private DbContextOptions options;
        private IMetadataProvider metadataProvider;
        private ITextSerializer serializer;

        public SqlMessageLog(DbContextOptions options, ITextSerializer serializer, IMetadataProvider metadataProvider)
        {
            this.options = options;
            this.serializer = serializer;
            this.metadataProvider = metadataProvider;
        }

        public void Save(IEvent @event)
        {
            using (var context = new MessageLogDbContext(this.options))
            {
                var metadata = this.metadataProvider.GetMetadata(@event);

                context.Set<MessageLogEntity>().Add(new MessageLogEntity
                {
                    Id = Guid.NewGuid(),
                    SourceId = @event.SourceId.ToString(),
                    Kind = metadata.GetValue(StandardMetadata.Kind),
                    AssemblyName = metadata.GetValue(StandardMetadata.AssemblyName),
                    FullName = metadata.GetValue(StandardMetadata.FullName),
                    Namespace = metadata.GetValue(StandardMetadata.Namespace),
                    TypeName = metadata.GetValue(StandardMetadata.TypeName),
                    SourceType = metadata.GetValue(StandardMetadata.SourceType),
                    CreationDate = DateTime.UtcNow.ToString("o"),
                    Payload = serializer.Serialize(@event),
                });
                context.SaveChanges();
            }
        }

        public void Save(ICommand command)
        {
            using (var context = new MessageLogDbContext(this.options))
            {
                var metadata = this.metadataProvider.GetMetadata(command);

                context.Set<MessageLogEntity>().Add(new MessageLogEntity
                {
                    Id = Guid.NewGuid(),
                    SourceId = command.Id.ToString(),
                    Kind = metadata.GetValue(StandardMetadata.Kind),
                    AssemblyName = metadata.GetValue(StandardMetadata.AssemblyName),
                    FullName = metadata.GetValue(StandardMetadata.FullName),
                    Namespace = metadata.GetValue(StandardMetadata.Namespace),
                    TypeName = metadata.GetValue(StandardMetadata.TypeName),
                    SourceType = metadata.GetValue(StandardMetadata.SourceType),
                    CreationDate = DateTime.UtcNow.ToString("o"),
                    Payload = serializer.Serialize(command),
                });
                context.SaveChanges();
            }
        }

        public IEnumerable<IEvent> Query(QueryCriteria criteria)
        {
            return new SqlQuery(this.options, this.serializer, criteria);
        }

        private class SqlQuery : IEnumerable<IEvent>
        {
            private DbContextOptions options;
            private ITextSerializer serializer;
            private QueryCriteria criteria;

            public SqlQuery(DbContextOptions options, ITextSerializer serializer, QueryCriteria criteria)
            {
                this.options = options;
                this.serializer = serializer;
                this.criteria = criteria;
            }

            public IEnumerator<IEvent> GetEnumerator()
            {
                return new DisposingEnumerator(this);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            private class DisposingEnumerator : IEnumerator<IEvent>
            {
                private SqlQuery sqlQuery;
                private MessageLogDbContext context;
                private IEnumerator<IEvent> events;

                public DisposingEnumerator(SqlQuery sqlQuery)
                {
                    this.sqlQuery = sqlQuery;
                }

                ~DisposingEnumerator()
                {
                    if (context != null) context.Dispose();
                }

                public void Dispose()
                {
                    if (context != null)
                    {
                        context.Dispose();
                        context = null;
                        GC.SuppressFinalize(this);
                    }
                    if (events != null)
                    {
                        events.Dispose();
                    }
                }

                public IEvent Current { get { return events.Current; } }
                object IEnumerator.Current { get { return this.Current; } }

                public bool MoveNext()
                {
                    if (context == null)
                    {
                        context = new MessageLogDbContext(sqlQuery.options);
                        var queryable = context.Set<MessageLogEntity>().AsQueryable()
                            .Where(x => x.Kind == StandardMetadata.EventKind);

                        var where = sqlQuery.criteria.ToExpression();
                        if (where != null)
                            queryable = queryable.Where(where);

                        events = queryable
                            .AsEnumerable()
                            .Select(x => this.sqlQuery.serializer.Deserialize<IEvent>(x.Payload))
                            .GetEnumerator();
                    }

                    return events.MoveNext();
                }

                public void Reset()
                {
                    throw new NotSupportedException();
                }
            }
        }
    }
}
