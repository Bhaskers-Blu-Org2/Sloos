// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Data.Entity.Design.PluralizationServices;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CSharp;
using Microsoft.EntityFrameworkCore;

namespace Sloos
{
    public class Context : DbContext
    {
        public Context(string nameOfConnectionString)
            : base(new DbContextOptions<Context>())
        {
            var opts = (new DbContextOptionsBuilder())
                .UseSqlServer("connectionString");
        }

        public Context(DbContextOptions options)
            : base(options)
        {
        }
    }

    public class Column
    {
        public string Name { get; set; }
        public int Offset { get; set; }
        public string TypeName { get; set; }
    }

    public class EntityPump
    {
        public string TableName { get; set; }
        public Type EntityType { get; set; }
        public Type ContextType { get; set; }
    }

    class ContextFactory
    {
        private readonly ContextTemplate context;

        public ContextFactory(
            string typeName,
            Column[] columns)
        {
            this.context = this.Construct(typeName, columns);
        }

        public ContextFactory(
            string typeName,
            string schema)
        {
            var columns = schema
                .Split(',')
                .Select(x => x.Split(':'))
                .Select((x, i) => new Column
                {
                    Name = x[0],
                    TypeName = x[1],
                    Offset = i,
                })
                .ToArray();

            this.context = this.Construct(typeName, columns);
        }

        private ContextTemplate Construct(string typeName, Column[] columns)
        {
            var cxt = new ContextTemplate
            {
                Key = "ID",
                Columns = columns,
            };

            var service = PluralizationService.CreateService(CultureInfo.CurrentCulture);
            if (service.IsPlural(typeName))
            {
                cxt.RowName = service.Singularize(typeName);
                cxt.TableName = typeName;
            }
            else
            {
                cxt.RowName = typeName;
                cxt.TableName = service.Pluralize(typeName);
            }

            return cxt;
        }

        public EntityPump BuildAssembly(
            AssemblyName assemblyName)
            
        {
            string code = this.context.TransformText();

            using (var codeProvider = new CSharpCodeProvider(
                new Dictionary<string, string> { { "CompilerVersion", "v4.0" } }))
            {
                var path = Path.GetDirectoryName(
                    Assembly.GetExecutingAssembly().Location);

                var assemblies = new List<string>
                {
                    "netstandard.dll",
                    "System.dll",
                    "System.ComponentModel.DataAnnotations.dll",
                    "System.Core.dll",
                    "System.Data.dll",
                    "System.Data.Entity.dll",
                    "System.Runtime.Serialization.dll",
                    Assembly.GetExecutingAssembly().Location,
                    Path.Combine(path, "Microsoft.EntityFrameworkCore.dll"),
                    Path.Combine(path, "Microsoft.EntityFrameworkCore.Relational.dll"), // DbContext.Database.Migrate()
                    //Path.Combine(path, "Microsoft.EntityFrameworkCore.Abstractions.dll"),
                    //Path.Combine(path, "Microsoft.Extensions.Caching.Abstractions.dll"),
                    //Path.Combine(path, "Microsoft.Extensions.Caching.Memory.dll"),
                    //Path.Combine(path, "Microsoft.Extensions.Configuration.Abstractions.xml"),
                    //Path.Combine(path, "Microsoft.Extensions.Configuration.Binder.xml"),
                };

                var options = new CompilerParameters(
                    assemblies.ToArray(),
                    assemblyName.CodeBase,
                    true)
                {
                    //CompilerOptions = string.Format(@"/optimize /lib:""{0}""", path),
                    //CompilerOptions = string.Format(@"/target:library"),
                    GenerateExecutable = false,
                    GenerateInMemory = true,
                    OutputAssembly = "Spike.CodeGen.dll",
                };

                CompilerResults results = codeProvider.CompileAssemblyFromSource(options, code);
                if (results.Errors.Count > 0)
                {
                    string message = $"Cannot compile typed context: {results.Errors[0].ErrorText} (line {results.Errors[0].Line})";
                    throw new Exception(message);
                }
            }

            var assembly = Assembly.Load(assemblyName);
            var entityType = assembly.DefinedTypes.First(x => x.Name == this.context.RowName);
            var contextType = assembly.DefinedTypes.First(x => x.Name == "Context");

            var pump = new EntityPump()
            {
                TableName = this.context.TableName,
                EntityType = entityType,
                ContextType = contextType,
            };

            return pump;
        }
    }
}
