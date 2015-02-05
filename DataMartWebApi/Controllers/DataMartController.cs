using Nest;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Web.Http;
using System.Configuration;

namespace DataMartWebApi.Controllers
{
    public class DataMartController : ApiController
    {
        private string matchAllQuery = "{" + '\u0022' + "match_all" + '\u0022' + ": {}}";
        private ElasticClient esClient = null;
        private ConnectionSettings myConnection = null;

        public DataMartController()
        {
            Uri esServer = new Uri(ConfigurationManager.AppSettings["ElasticSearchServerUrl"]);
            int timeout;
            Int32.TryParse(ConfigurationManager.AppSettings["ESConnectionTimeoutInMinutes"], out timeout);
            myConnection = new ConnectionSettings(esServer);
            myConnection.SetTimeout(1000 * 60 * timeout); // connection timeout
            esClient = new ElasticClient(myConnection);
        }

        /// <summary>
        /// Get all the Indices for a given elastic search server
        /// </summary>
        [HttpGet]
        [ActionName("getindices")]
        public IEnumerable<string> GetIndices()
        {
            var results = esClient.Raw.IndicesStatusForAll();
            var dictionaryResponse = results.Response;
            dynamic value = null;
            dictionaryResponse.TryGetValue("Indices", out value);
            string jsonIndices = value.ToString();

            // now we have all the top level indices in "jsonIndices"
            JObject indices = JObject.Parse(jsonIndices);
            IList<string> indicesList = new List<string>();
            foreach (var child in indices.Children())
            {
                // Remove all the ES infrastructure related indices that we don't care about
                if (!child.Path.StartsWith(".") && !child.Path.StartsWith("_"))
                {
                    yield return child.Path;
                }
            }
        }

        /// <summary>
        /// Get all the document types for a given index
        /// </summary>
        [HttpGet]
        [ActionName("gettypes")]
        public IEnumerable<string> GetTypes(string indexName)
        {
            var results = esClient.Raw.IndicesGetMapping(indexName);
            var dictionaryResponse = results.Response;
            dynamic value = null;
            dictionaryResponse.TryGetValue(indexName, out value);
            string jsonTypes = value.ToString();
            JObject types = JObject.Parse(jsonTypes);
            IList<string> typesList = new List<string>();
            foreach (var child in types.Children())
            {
                foreach (var subchild in child.Children())
                {
                    JObject docType = JObject.Parse(subchild.ToString());
                    foreach (var type in docType.Children())
                    {
                        yield return type.Path;
                    }
                }
            }
        }

        /// <summary>
        /// Get all the documents for a given index, from / to range, and option ES Query and Document Type
        /// </summary>
        [HttpGet]
        [ActionName("getdocument")]
        public IEnumerable<JObject> GetDocument(string indexName, string dataSourceName = "", int from = 0, int size = (Int32.MaxValue - 1), string query = "")
        {
            if (String.IsNullOrEmpty(query))
            {
                query = matchAllQuery;
            }

            if (String.IsNullOrEmpty(dataSourceName))
            {
                var results = esClient.Search<JObject>(x => x.Index(indexName).From(from).Size(size).AllTypes().
                  QueryRaw(query));
                return results.Documents;
            }
            else
            {
                var results = esClient.Search<JObject>(x => x.Index(indexName).From(from).Size(size).
                  Type(dataSourceName).QueryRaw(query));
                return results.Documents;
            }
        }

        /// <summary>
        /// We offer a POST Web API method to in case the query string for example is too big for a GET query
        /// query against a given indexName name with a specified from / to range and a JSONQuery and optionally a document type
        /// </summary>
        [ActionName("rawquery")]
        [HttpPost]
        public IEnumerable<JObject> PostRawQuery(string indexName, string dataSourceName, int from, int size, [FromBody]string JSONQuery)
        {
            if (String.IsNullOrEmpty(dataSourceName))
            {
                var results = esClient.Search<JObject>(x => x.Index(indexName).From(from).Size(size).AllTypes().
                QueryRaw(JSONQuery));
                return results.Documents;
            }
            else
            {
                var results = esClient.Search<JObject>(x => x.Index(indexName).From(from).Size(size).
                    Type(dataSourceName).QueryRaw(JSONQuery));
                return results.Documents;
            }
        }
    }
}