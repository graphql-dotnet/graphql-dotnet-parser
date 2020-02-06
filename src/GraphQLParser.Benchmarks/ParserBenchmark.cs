﻿using BenchmarkDotNet.Attributes;
using System.Collections.Generic;

namespace GraphQLParser.Benchmarks
{
    [MemoryDiagnoser]
    [RPlotExporter, CsvMeasurementsExporter]
    public class ParserBenchmark
    {
        private ILexemeCache? _serial;
        private ILexemeCache? _concurrent;

        [GlobalSetup]
        public void GlobalSetup()
        {
            _serial = new DictionaryCache();
            _concurrent = new ConcurrentDictionaryCache();
        }

        [Benchmark(Baseline = true)]
        [ArgumentsSource(nameof(Queries))]
        public void Parse(Query query)
        {
            var parser = new Parser(new Lexer());
            parser.Parse(new Source(query.Text));
        }

        [Benchmark]
        [ArgumentsSource(nameof(Queries))]
        public void Serial(Query query)
        {
            var parser = new Parser(new Lexer() { Cache = _serial });
            parser.Parse(new Source(query.Text));
        }

        [Benchmark]
        [ArgumentsSource(nameof(Queries))]
        public void Concurrent(Query query)
        {
            var parser = new Parser(new Lexer() { Cache = _concurrent });
            parser.Parse(new Source(query.Text));
        }

        public IEnumerable<Query> Queries()
        {
            yield return new Query { Name = "Simple", Text = SMALL_QUERY };
            yield return new Query { Name = "Schema", Text = INTROSPECTION_QUERY };
            yield return new Query { Name = "Params", Text = PARAMS_QUERY };
        }

        public struct Query
        {
            public string Text;
            public string Name;
            public override string ToString() => Name;
        }

        private const string SMALL_QUERY = "query test { field1 field2(id: 5) { name address } field3 }";
        private const string PARAMS_QUERY = @"query { something(name: ""one"", names: [""abc"", ""def"", ""klmn"", ""abra"", ""blabla"", ""kadabra"", ""100500""] code: 123, values: [1,2,3,4,5,6,7,8,9,0,10,20,30,40,50,60,70,80,90,100], modified: true, percents: [10.1, 20.2, 30.3, 40.4, 50.5, 60.6, 70.7], mask: [true, false, true, false, true, false], struct: { name: ""tom"", age: 42, height: 1.82, friends: [ { name: ""nik"" }, { name: ""ben"" }]}) }";
        private const string INTROSPECTION_QUERY = @"
  query IntrospectionQuery {
    __schema {
      queryType { name }
      mutationType { name }
      subscriptionType { name }
      types {
        ...FullType
      }
      directives {
        name
        description
        locations
        args {
          ...InputValue
        }
      }
    }
  }

  fragment FullType on __Type {
    kind
    name
    description
    fields(includeDeprecated: true) {
      name
      description
      args {
        ...InputValue
      }
      type {
        ...TypeRef
      }
      isDeprecated
      deprecationReason
      directives {
        name
        args {
          name
          value
        }
      }
    }
    inputFields {
      ...InputValue
    }
    interfaces {
      ...TypeRef
    }
    enumValues(includeDeprecated: true) {
      name
      description
      isDeprecated
      deprecationReason
    }
    possibleTypes {
      ...TypeRef
    }
  }

  fragment InputValue on __InputValue {
    name
    description
    type { ...TypeRef }
    defaultValue
  }

  fragment TypeRef on __Type {
    kind
    name
    ofType {
      kind
      name
      ofType {
        kind
        name
        ofType {
          kind
          name
          ofType {
            kind
            name
            ofType {
              kind
              name
              ofType {
                kind
                name
                ofType {
                  kind
                  name
                }
              }
            }
          }
        }
      }
    }
  }
";
    }
}