using System;
using System.Collections.Generic;
using System.Text;
using System.Management;
using System.Net;
using TradeLink.Common;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// co-author: "DimOn" <tradeR?lab@gmail.com>





// mongodb: as a minimum:
using MongoDB.Bson;
using MongoDB.Driver;   // http://docs.mongodb.org/ecosystem/tutorial/use-csharp-driver/
// Additionally, you will frequently add one or more of these using statements:
using MongoDB.Driver.Builders;
using MongoDB.Driver.GridFS;
using MongoDB.Driver.Linq;
using MongoDB.Bson.Serialization;    // otherwise error: The name 'BsonSerializer' does not exist in the current context
// http://docs.mongodb.org/ecosystem/tutorial/getting-started-with-csharp-driver/#getting-started-with-csharp-driver

namespace TradeLink.AppKit
{
    public static class MyGlobals
    {
        public static string[] args;
    }

    public class ResponseParametersHolder
    {
        public
            MongoDB.Bson.BsonDocument bson = null;

        public ResponseParametersHolder()
        {
        }

        public int parse_json_file(string json_full_filename)
        {
            if (json_full_filename != "")
            {
                bool json_file_exist = System.IO.File.Exists(json_full_filename);

                if (json_file_exist)
                {
                    // read file
                    string json_str = System.IO.File.ReadAllText(@json_full_filename);

                    // parse and deserialize bson
                    bson = BsonSerializer.Deserialize<BsonDocument>(json_str);

                    return 0;
                }
            }
            return 1;
        }

        public string get_as_json()
        {
            // add error check (catch exception)
            return bson.ToJson();

            //return JsonConvert.SerializeObject(response_parameters);
        }

        public int write_json_to_file(string json_full_filename)
        {
            // add error check (catch exception)
            System.IO.File.WriteAllText(json_full_filename, get_as_json());
            return 0;
        }
    }

    
    
    
    
    
    
    
    
    
    
    
    public class UserJsonParameters
    {
        public string[] tick_files { get; set; }
        //public DateTime ExpiryDate { get; set; }
        public string response_dll { get; set; }
        public string response_class { get; set; }
        
        public int chart_barsize_s { get; set; }
        public string chart_candle_checkbox { get; set; }
        public string chart_y_axix_scale_to_visible_prices_checkbox { get; set; }

        public string play_to { get; set; }
    }

    public class UserJsonParameters_Loader
    {
        public bool json_file_exist { get; set; }
        public string json_as_str { get; set; }
        public UserJsonParameters user_json_parameters;

        public UserJsonParameters_Loader() : this(""){}
        public UserJsonParameters_Loader(string json_full_filename)
        //load_defaults_from_kadina_json
        {
            string log_prefix = "TradeLink.AppKit::UserJsonParameters::load_defaults_from_kadina_json(): ";

            // check if kadina.json file exist
            if (json_full_filename != "")
            {
                json_file_exist = System.IO.File.Exists(json_full_filename);

                //debug(log_prefix + "checking if json_file_exist = " + json_file_exist);
                if (json_file_exist)
                {
                    // read file
                    //debug(log_prefix + " reading json config file");
                    json_as_str = System.IO.File.ReadAllText(@json_full_filename);
                    //debug(log_prefix + " read " + json_as_str.Length + " bytes");

                    // parse file (todo: add error check)
                    //debug(log_prefix + " parsing json");
                    //var deserializedProduct = JsonConvert.DeserializeObject<user_json_parameters>(json_as_str);
                    user_json_parameters = JsonConvert.DeserializeObject<UserJsonParameters>(json_as_str);
                }
            }
        }
    }
}




//// this example works! http://blog.programmingsolution.net/c-sharp/json-to-object-conversion-in-c-sharp/
//JObject jsonObj = JObject.Parse(json_as_str);
//string report_id = (string)(jsonObj["report_id"]);
//string report_name = (string)jsonObj["report_name"];