using System;
using System.Collections.Generic;
using System.Text;
using System.Management;
using System.Net;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// author: "DimOn" <tradelab@gmail.com>

//namespace TradeLink.AppKit
//{
    public static class MyGlobals
    {
        public static string[] args;
    }

    class StrInt
    {
        public string key { get; set; }
        public int val { get; set; }
    }

    public class ResponseParametersHolder
    {
        public
            Dictionary<string, string> response_parameters = new Dictionary<string, string>();

        public ResponseParametersHolder()
        {
        }

        public int parse_json_file(string json_full_filename)
        {
            if (json_full_filename != "")
            {
                bool json_file_exist = System.IO.File.Exists(json_full_filename);

                //debug(log_prefix + "checking if json_file_exist = " + json_file_exist);
                if (json_file_exist)
                {
                    // read file
                    //debug(log_prefix + " reading json config file");
                    string json_as_str = System.IO.File.ReadAllText(@json_full_filename);
                    //debug(log_prefix + " read " + json_as_str.Length + " bytes");

                    // parse file (todo: add error check)
                    //debug(log_prefix + " parsing json");
                    //var deserializedProduct = JsonConvert.DeserializeObject<user_json_parameters>(json_as_str);
                    response_parameters = JsonConvert.DeserializeObject<Dictionary<string, string>>(json_as_str);

                    //Dictionary<string, JObject> asdf = JsonConvert.DeserializeObject<Dictionary<string, JObject>>(json_as_str);
                    



                    //// this example works! http://blog.programmingsolution.net/c-sharp/json-to-object-conversion-in-c-sharp/
                    //JObject jsonObj = JObject.Parse(json_as_str);
                    //string report_id = (string)(jsonObj["report_id"]);
                    //string report_name = (string)jsonObj["report_name"];





                    //    http://serega41.dyndns.org:55555/svn/tradelink/trunk
                    // http://tortoisesvn.net/docs/release/TortoiseSVN_en/tsvn-dug-branchtag.html
                    return 0;
                }
            }
            return 1;
        }

        public string get_as_json()
        {
            // add error check (catch exception)
            return JsonConvert.SerializeObject(response_parameters);
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

//}
