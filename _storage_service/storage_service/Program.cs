using System;

using System.Threading; // for threads

using System.Collections.Generic;
using System.Linq;
using System.Text;

using NDesk.Options; // for cmd-line args parsing

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// these are for rabbitmq
using System.Text;              // system.text for Encoding()
using RabbitMQ.Client;          // 
using RabbitMQ.Client.Events;   // for BasicDeliverEventArgs

// mongodb: as a minimum:
using MongoDB.Bson;
using MongoDB.Driver;   // http://docs.mongodb.org/ecosystem/tutorial/use-csharp-driver/
// Additionally, you will frequently add one or more of these using statements:
using MongoDB.Driver.Builders;
using MongoDB.Driver.GridFS;
using MongoDB.Driver.Linq;
using MongoDB.Bson.Serialization;
// http://docs.mongodb.org/ecosystem/tutorial/getting-started-with-csharp-driver/#getting-started-with-csharp-driver

using System.Diagnostics;   // for stopwatch: http://stackoverflow.com/questions/28637/is-datetime-now-the-best-way-to-measure-a-functions-performance

using MongoDB.Bson.Serialization; 

namespace storage_service
{
    public class Program
    {
        static void Main(string[] args)
        {
            StorageService ss = new StorageService();
            ss.main(args);
            return;
        }
    }

    // example on threading in c# was found here: http://msdn.microsoft.com/en-us/library/7a2f3ay4(v=vs.90).aspx
    public class Worker_console_hartbeat_progressbar
    {
        // This method will be called when the thread is started. 
        public void DoWork()
        {
            string[] progress_array = {"\\","|", "/", "-"};
            Thread.Sleep(1000); // wait 1sec before drawing dynamic progress bar
            while (!_shouldStop)
            {
                
                int max_rigth_pos = 70;

                // ?wtf for (int i = 0; i < max_right_pos + 1; i++) Console.Write(" ");
                Console.Write("\r");
                for (int i = 0; i < 71; i++) Console.Write(" ");

                for (int cur_pos = 1; cur_pos < max_rigth_pos; cur_pos++)
                {
                    Console.Write("\r");
                    for(int i=0; i<cur_pos; i++) Console.Write(" ");
                    //Console.Write(progress_array[((cur_pos - 1) % progress_array.Length)] + progress_array[cur_pos % progress_array.Length] + " ");
                    Console.Write( progress_array[cur_pos % progress_array.Length] + " ");

                    Thread.Sleep(100);
                }
            }
            Console.WriteLine("worker thread: terminating gracefully.");
        }
        public void RequestStop()
        {
            _shouldStop = true;
        }
        // Volatile is used as hint to the compiler that this data 
        // member will be accessed by multiple threads. 
        //private volatile bool _shouldStop;
        public volatile bool _shouldStop;
        public int counter;
    }



    public class StorageService
    {
        // rabbitmq stuff
        public Dictionary<string, string> storage_service_parameters = null;
        public Dictionary<string, string> rabbitmq_parameters = null;

        // mongodb stuff
        public Dictionary<string, string> mongodb_parameters = null;
        public string mongodb_connection_string;
        public string mongodb_database_name;
        public string mongodb_collection_name;

        // just a counter (for debug)
        Int64 message_counter = 0;
        //int report_each_N_messages = 1000;

        MongoCollection<BsonDocument> mongodb_collection;

        ResponseParametersHolder parameters_holder = null;

        //
        // CALLBACK THING:
        //
        private void my_callback(IBasicConsumer consumer,
                                     BasicDeliverEventArgs eargs)
        {
            IBasicProperties msg_props = eargs.BasicProperties;
            String msg_body_str = Encoding.ASCII.GetString( eargs.Body );
            BsonDocument msg_body_bson = BsonDocument.Parse( msg_body_str );

            // insert bson object to mongodb
            mongodb_collection.Insert(msg_body_bson);

            // 
            message_counter++;

            if (true) //if (message_counter % report_each_N_messages == 0)
            {
                // Console.WriteLine("\nrecevied another " + report_each_N_messages + " messages: message_counter=" + message_counter + "\n\n" + msg_body_bson.ToString());
                Console.Write("\rmessage_counter: " + message_counter + "                                  ");
            }
        }

  

        public void main(string[] args)
        {

            // ============================================================================
            // http://stackoverflow.com/questions/5624934/convert-string-into-mongodb-bsondocument
            string json_str = @"{ 
                'some_str' : 'bar', 
                'int_arr' : [1,2,3,4,5],
                'int_val' : 1,
                'decimal_val' : 99.99,
                'double_val' : 88.88
            }";
            // case 1 of 2: we convert json string to bson document
            MongoDB.Bson.BsonDocument bson1
                = BsonSerializer.Deserialize<BsonDocument>(json_str);

            // case 2 of 2: we convert json string to bson document (same, but more elegant)
            //
            BsonDocument bson2 = BsonDocument.Parse(json_str);

            // now we extract values from bson document. Below you'll find several examples (by different data types)
            //
            string some_str3 = BsonSerializer.Deserialize<string>(bson2["some_str"].ToJson());
            string some_str = BsonSerializer.Deserialize<string>(bson2["some_str"].ToJson());
            int[] int_arr = BsonSerializer.Deserialize<int[]>(bson2["int_arr"].ToJson());
            int int_val = BsonSerializer.Deserialize<int>(bson2["int_val"].ToJson());
            decimal decimal_val = BsonSerializer.Deserialize<decimal>(bson2["decimal_val"].ToJson());
            double double_val = BsonSerializer.Deserialize<double>(bson2["double_val"].ToJson());

            // now same action, but using Json.NET (imho this way is better (more readable and not mongo-library dependant)
            // todo: if we choose this way make sure it's bson docs are really compatible with mongo bson docs.
            // OR may be I like the 1st way better now ...  ;)
            //
            JToken root = JObject.Parse(json_str);

            JToken some_str2_token = root["some_str"];
            string some_str2 = some_str2_token.ToString();

            JToken int_arr_token = root["int_arr"];
            int[] int_arr2 = JsonConvert.DeserializeObject<int[]>(int_arr_token.ToString());

            JToken int_val_token = root["int_val"];
            int int_val2 = JsonConvert.DeserializeObject<int>(int_val_token.ToString());

            JToken decimal_val_token = root["decimal_val"];
            decimal decimal_val2 = JsonConvert.DeserializeObject<decimal>(decimal_val_token.ToString());

            JToken double_val_token = root["double_val"];
            double double_val2 = JsonConvert.DeserializeObject<double>(double_val_token.ToString());

            // noop just for breakpoint ;)
            ((Action)(() => { }))();

            //
            // ============================================================================
            // [on topic of Query Mongo]
            // How to deserialize a BsonDocument object back to class
            // http://stackoverflow.com/questions/9478613/how-to-deserialize-a-bsondocument-object-back-to-class
            //
            //QueryDocument _document = new QueryDocument("key", "value");

            //MongoCursor<BsonDocument> _documentsReturned =
            //                          _collection.FindAs<BsonDocument>(_document);

            //foreach (BsonDocument _document1 in _wordOntologies)
            //{
            //    //deserialize _document1
            //    //?
            //}
            // ============================================================================
            // [q] how to create new BsonDocument?
            // [a]
            //var document = new BsonDocument {
            //    { "author", "joe" },
            //    { "title", "yet another blog post" },
            //    { "text", "here is the text..." },
            //    { "tags", new BsonArray { "example", "joe" } },
            //    { "comments", new BsonArray {
            //        new BsonDocument { { "author", "jim" }, { "comment", "I disagree" } },
            //        new BsonDocument { { "author", "nancy" }, { "comment", "Good post" } }
            //    }}
            //};
            //// ============================================================================
            // MongoDB C# - Getting BsonDocument for an Element that doesn't exist
            // http://stackoverflow.com/questions/6628794/mongodb-c-sharp-getting-bsondocument-for-an-element-that-doesnt-exist
            // There is also an overload that lets you provide a default value:
            //
            // BsonDocument document;
            // var firstName = (string)document["FirstName", null];
            // // or
            // var firstName = (string)document["FirstName", "N/A"];
            //
            // OR
            //
            // var b = new BsonDocument();
            // var exists = b.Contains("asdfasdf");
            //// ============================================================================

            //MyGlobals.args = args;
            // .... and then in any other place in app we can access global args:
            //string[] args = MyGlobals.args;

            string storage_service_config_json = "";
            string rabbitmq_config_json = "";
            string mongodb_config_json = "";

            var p = new OptionSet() {
               { "c|storage-service-config=", "path to storage_service json configuration file",
                  v => storage_service_config_json = v},
               {"r|rabbitmq-config=", "path to rabbitmq json configuration file",
                  v => rabbitmq_config_json = v},
               {"m|mongodb-config=", "path to mongodb json configuration file",
                  v => mongodb_config_json = v}
            };

            // parse  cmd-line args
            List<string> extra;
            try
            {
                extra = p.Parse(args);
            }
            catch (OptionException e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `--help' for more information.");
                return;
            }

            // get parameters documents
            if (storage_service_config_json != "")
            {
                parameters_holder = new ResponseParametersHolder();
                parameters_holder.parse_json_file(storage_service_config_json);
                storage_service_parameters = parameters_holder.response_parameters;

                parameters_holder = new ResponseParametersHolder();
                parameters_holder.parse_json_file(rabbitmq_config_json);
                rabbitmq_parameters = parameters_holder.response_parameters;

                parameters_holder = new ResponseParametersHolder();
                parameters_holder.parse_json_file(mongodb_config_json);
                mongodb_parameters = parameters_holder.response_parameters;

                //_ema_bar = Convert.ToInt32(storage_service_parameters["_ema_bar"]);
                //_response_barsize_s = Convert.ToInt32(storage_service_parameters["_response_barsize_s"]);
                //_stop_k = Convert.ToDouble(storage_service_parameters["_stop_k"]);
                //_target_price_k = Convert.ToDouble(storage_service_parameters["_target_price_k"]);

                // Custom name of response set by you
                //Name = storage_service_parameters["name"];

            }






            // extract individual rabbitmq parameters
            //
            string exchange_name = rabbitmq_parameters["exchange_name"];
            string exchange_type = rabbitmq_parameters["exchange_type"];
            ConnectionFactory conn_factory = new ConnectionFactory();
            conn_factory.HostName = rabbitmq_parameters["host_name"];
            conn_factory.UserName = rabbitmq_parameters["user_name"];
            conn_factory.Password = rabbitmq_parameters["password"];

            // extract storage_service parameters
            //
            string queue_name = storage_service_parameters["queue_name"];
            string routing_key = storage_service_parameters["routing_key"];   // no need to provide routing key for 'fanout' exchange. "rout";


            // extract mongodb parameters
            //
            mongodb_connection_string = mongodb_parameters["mongodb_connection_string"];
            mongodb_database_name = mongodb_parameters["mongodb_database_name"];
            mongodb_collection_name = mongodb_parameters["mongodb_collection_name"];
            
            //---------------------------------------------------------------- progress bar end --------------
            // Create the thread object. This does not start the thread.
            Worker_console_hartbeat_progressbar worker_console_hartbeat_progressbar = new Worker_console_hartbeat_progressbar();
            Thread workerThread = new Thread(worker_console_hartbeat_progressbar.DoWork);

            // Start the worker thread.
            workerThread.Start();
            //---------------------------------------------------------------- progress bar end --------------

            System.Console.WriteLine("Welcome to storage_service (part of tradelink.afterlife)!");
            System.Console.WriteLine("---------------------------------------------------------");
            System.Console.WriteLine("connecting to mongodb...");


            // prepare mongo
            MongoClient client = new MongoClient(mongodb_connection_string);
            MongoServer server = client.GetServer();
            MongoDatabase database = server.GetDatabase(mongodb_database_name); // "test" is the name of the database
            mongodb_collection = database.GetCollection<BsonDocument>(mongodb_collection_name);

            System.Console.WriteLine("connecting to rabbitmq... wwarning: sometimes it hungs here");

            // create rabbitmq connection
            IConnection conn = conn_factory.CreateConnection();
            // create channel
            IModel chan = conn.CreateModel();
            chan.ExchangeDeclare(exchange_name,
                     exchange_type,
                     false, // durable
                     false, // autodelete
                     null); // args
            chan.QueueDeclare(queue_name,
                              false,    // durable
                              false,    // exclusive
                              true,     // autodelete
                              null);    // args
            chan.QueueBind(queue_name, exchange_name, routing_key);

            EventingBasicConsumer c_consumer = new EventingBasicConsumer { Model = chan };
            c_consumer.Received += my_callback;
            System.Console.WriteLine("ready to consume messages...");

            // blocking call:
            chan.BasicConsume(queue_name,
                              false,
                              c_consumer);

            /*
            EventingBasicConsumer r_consumer = new EventingBasicConsumer {Model = chan};
            r_consumer.Received += rate_limit_notify;
            chan.BasicConsume("rate_limit",
                              false,
                              r_consumer);
             */

        }
    }
}
