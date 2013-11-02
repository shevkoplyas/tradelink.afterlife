using System;
using System.Collections.Generic;
//using System.Text;
using TradeLink.Common;
using TradeLink.API;

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



namespace TradeLink.Common
{
    /// <summary>
    /// Template for most typical response.  Inherit from this to create a symbol grey or black strategy.
    /// eg:
    /// public class MyStrategy : ResponseTemplate
    /// {
    /// }
    /// </summary>
    public class MessageBusableResponseTemplate : Response
    {
        // this enum defined here, since we don't use it anywhere else but in responses
        public enum MimeType {got_tick=1, got_order, got_fill, got_order_cancel, got_message, reset, 
            send_order, send_cancel, send_indicators, send_basket, send_ticket, 
            send_message, send_debug, send_chartlabel, got_position, shutdown,
            got_new_bar, custom };

        // this guid is generated once per new instance and remains the same foreva!-)
        // it can allow us to distinguish between two instances of the similar strategies 
        // working at the same time (may be even with the same symbol-set).
        string response_guid;

        //public MongoDB.Bson.BsonDocument storage_service_parameters_bson = null;
        public MongoDB.Bson.BsonDocument rabbitmq_parameters_bson = null;

        // mongodb channel
        public IModel mongodb_channel = null;
        IBasicProperties mongodb_msg_props = null;
        string exchange_name;

        public MessageBusableResponseTemplate()
            : base()
        {
            System.Guid guid = System.Guid.NewGuid();
            response_guid = guid.ToString();
        }
        //
        // This function (call_me_from_child_constructor()) would:
        //      - extract mongodb-related parameters
        //      - prepare mongodb connection (actually the result will be "mongodb_channel")
        //
        // It must be called from child class (from response itself) after child extracts rabbitmq_parameters_bson
        // document by parsing command line. This code can not go into constructor, since we need the child to take
        // some actions prior to this code. It is not elegant, but works!
        //
        public void call_me_from_child_constructor() // when stuff for messaging is parsed from cmd line
        {
            // extract rabbitmq parameters
            //
            exchange_name = BsonSerializer.Deserialize<string>(rabbitmq_parameters_bson["exchange_name"].ToJson());
            string exchange_type = BsonSerializer.Deserialize<string>(rabbitmq_parameters_bson["exchange_type"].ToJson());
            ConnectionFactory conn_factory = new ConnectionFactory();
            conn_factory.HostName = BsonSerializer.Deserialize<string>(rabbitmq_parameters_bson["host_name"].ToJson());
            conn_factory.UserName = BsonSerializer.Deserialize<string>(rabbitmq_parameters_bson["user_name"].ToJson());
            conn_factory.Password = BsonSerializer.Deserialize<string>(rabbitmq_parameters_bson["password"].ToJson());

            // extract required storage_service parameters
            //
            //string queue_name = storage_service_parameters["queue_name"];


            // create rabbitmq connection
            IConnection conn = conn_factory.CreateConnection();
            // create channel
            mongodb_channel = conn.CreateModel();

            // declare rabbitmq exchange
            mongodb_channel.ExchangeDeclare(exchange_name,
                     exchange_type,
                     false, // durable
                     false, // autodelete
                     null); // args

            mongodb_msg_props = mongodb_channel.CreateBasicProperties();
            mongodb_msg_props.ContentType = "application/json"; //  "text/plain";

        }

        // we use this for epoch sec calculations
        public DateTime origin_datetime_1970 = new DateTime(1970, 1, 1, 0, 0, 0, 0);       
        // http://stackoverflow.com/questions/3354893/how-can-i-convert-a-datetime-to-the-number-of-seconds-since-1970
        // http://stackoverflow.com/questions/2883576/how-do-you-convert-epoch-time-in-c
        
        public DateTime ConvertFromUnixTimestamp(double timestamp)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return origin.AddSeconds(timestamp);
        }

        public double ConvertToUnixTimestamp(DateTime date)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan diff = date.ToUniversalTime() - origin_datetime_1970;
            return Math.Floor(diff.TotalSeconds);
        }



        private long message_serial_number = 0; // unique increasing integer number (unique per each msg for one particular response)
        public void send_event(MimeType mime_type, string body_key, string body_json)
        {
            string mime_type_str = "response/" + mime_type.ToString("f");
            string rabbit_routingKey = "responses." + response_guid + ".events." + mime_type_str;
            mongodb_channel.BasicPublish(exchange_name,
                            rabbit_routingKey,
                            mongodb_msg_props,
                            Encoding.UTF8.GetBytes("{" +
                                "\"mime_type\": \"" + mime_type_str + "\", " +
                                "\"response_guid\": \"" + response_guid + "\", " +
                                "\"sn\": " + (message_serial_number++) + ", " +
                                "\"time_epoch_s\": " + ConvertToUnixTimestamp(DateTime.Now).ToString() + ", " +
                                "\"" + body_key + "\": " + body_json +
                                "}"));

            // Encoding.UTF8.GetBytes("{ \"message\": \"" + i + ") hi from c# " + rabbit_message + "\", \"i_squared\": " + (i * i) + "}"));    // or Encoding.UTF8.GetBytes();  - we convert message into a byte array 

        }





        /// <summary>
        /// Called when new ticks are recieved
        /// here is where you respond to ticks, eg to populate a barlist
        /// this.MyBarList.newTick(tick);
        /// </summary>
        /// <param name="tick"></param>
        public virtual void GotTick(Tick k)
        {
            send_event(MimeType.got_tick, "tick", k.ToJson());
        }
        /// <summary>
        /// Called when new orders received
        /// track or respond to orders here, eg:
        /// this.MyOrders.Add(order);
        /// </summary>
        /// <param name="order"></param>
        public virtual void GotOrder(Order o)
        {
            send_event(MimeType.got_order, "order", o.ToJson());
        }
        /// <summary>
        /// Called when orders are filled as trades.
        /// track or respond to trades here, eg:
        /// positionTracker.Adjust(fill);
        /// </summary>
        /// <param name="fill"></param>
        public virtual void GotFill(Trade f)
        {
            send_event(MimeType.got_fill, "trade", f.ToJson());
        }
        /// <summary>
        /// Called if a cancel has been processed
        /// </summary>
        /// <param name="cancelid"></param>
        public virtual void GotOrderCancel(long id)
        {
            send_event(MimeType.got_order_cancel, "id", id.ToJson());
        }
        /// <summary>
        /// called when unknown message arrives.   
        /// </summary>
        /// <param name="type"></param>
        /// <param name="id"></param>
        /// <param name="data"></param>
        public virtual void GotMessage(MessageTypes type, long source, long dest, long msgid, string request, ref string response)
        {
            string msg_json = "{\"type\": \"" + type.ToString("f") + "\", \"source\": " + source.ToString() + ", \"dest\": " + dest.ToString() + ", \"msgid\": " + msgid.ToString() + ", \"request\": \"" + request + "\",  \"response\": \"" + response + "\"}";
            send_event(MimeType.got_message, "message", msg_json);
        }
        /// <summary>
        /// Call this to reset your response parameters.
        /// You might need to reset groups of indicators or internal counters.
        /// eg : MovingAverage = 0;
        /// </summary>
        public virtual void Reset()
        {
            send_event(MimeType.reset, "foo", "boo".ToJson());
        }
        /// <summary>
        /// short form of senddebug
        /// </summary>
        /// <param name="msg"></param>
        public virtual void D(string msg) { if (SendDebugEvent != null) SendDebugEvent(msg); }
        /// <summary>
        /// short form of sendorder
        /// </summary>
        /// <param name="o"></param>
        public virtual void O(Order o) { o.VirtualOwner = ID; if (SendOrderEvent != null) SendOrderEvent(o, ID); }
        /// <summary>
        /// short form of sendcancel
        /// </summary>
        /// <param name="id"></param>
        public virtual void C(long id) { if (SendCancelEvent != null) SendCancelEvent(id, ID); }
        /// <summary>
        /// short form of sendindicator
        /// </summary>
        /// <param name="indicators"></param>
        public virtual void I(string indicators) { if (SendIndicatorsEvent != null) SendIndicatorsEvent(ID, indicators); }
        /// <summary>
        /// short form of sendindicator
        /// </summary>
        /// <param name="indicators"></param>
        public virtual void I(object[] indicators) { string[] s = new string[indicators.Length]; for (int i = 0; i < indicators.Length; i++) s[i] = indicators[i].ToString(); SendIndicatorsEvent(ID, string.Join(",", s)); }
        /// <summary>
        /// short form of sendindicator
        /// </summary>
        /// <param name="indicators"></param>
        public virtual void I(string[] indicators) { if (SendIndicatorsEvent != null) SendIndicatorsEvent(ID, string.Join(",", indicators)); }
        /// <summary>
        /// sends an order
        /// </summary>
        /// <param name="o"></param>
        public virtual void sendorder(Order o) 
        {
            send_event(MimeType.send_order, "order", o.ToJson());
            o.VirtualOwner = ID; if (SendOrderEvent != null) SendOrderEvent(o, ID); 
        }
        /// <summary>
        /// cancels an order (must have the id)
        /// </summary>
        /// <param name="id"></param>
        public virtual void sendcancel(long id) 
        {
            send_event(MimeType.send_cancel, "id", id.ToJson());
            if (SendCancelEvent != null) SendCancelEvent(id, ID); 
        }
        /// <summary>
        /// sends indicators as array of objects for later analysis
        /// </summary>
        /// <param name="indicators"></param>
        public virtual void sendindicators(object[] indicators) 
        {
            send_event(MimeType.send_indicators, "indicators", indicators.ToJson());
            string[] s = new string[indicators.Length]; for (int i = 0; i < indicators.Length; i++) s[i] = indicators[i].ToString(); if (SendIndicatorsEvent != null) SendIndicatorsEvent(ID, string.Join(",", s)); 
        }
        /// <summary>
        /// send indicators as array of strings for later analysis
        /// </summary>
        /// <param name="indicators"></param>
        public virtual void sendindicators(string[] indicators)
        {
            send_event(MimeType.send_indicators, "indicators", indicators.ToJson());
            if (SendIndicatorsEvent != null) SendIndicatorsEvent(ID, string.Join(",", indicators)); 
        }
        /// <summary>
        /// sends indicators as a comma seperated string (for later analsis)
        /// </summary>
        /// <param name="indicators"></param>
        public virtual void sendindicators(string indicators) 
        {
            send_event(MimeType.send_indicators, "indicators", indicators.ToJson());
            if (SendIndicatorsEvent != null) SendIndicatorsEvent(ID, indicators);
        }
        /// <summary>
        /// requests ticks for a basket of securities
        /// </summary>
        /// <param name="syms"></param>
        public virtual void sendbasket(string[] syms) 
        {
            send_event(MimeType.send_basket, "syms", syms.ToJson());
            if (SendBasketEvent != null) SendBasketEvent(new BasketImpl(syms), ID); else senddebug("SendBasket not supported in this application."); 
        }
        /// <summary>
        /// request ticks for a basket of securities
        /// </summary>
        /// <param name="syms"></param>
        public virtual void sendbasket(Basket syms)
        {
            send_event(MimeType.send_basket, "syms", syms.ToJson());
            if (SendBasketEvent != null)
                SendBasketEvent(syms, ID);
            else
                senddebug("SendBasket not supported in this application.");
        }
        /// <summary>
        /// requests ticks for basket of securities
        /// </summary>
        /// <param name="syms"></param>
        public virtual void SB(string[] syms) { sendbasket(syms); }

        public event TicketDelegate SendTicketEvent;
        /// <summary>
        /// send ticket
        /// </summary>
        /// <param name="space"></param>
        /// <param name="user"></param>
        /// <param name="pw"></param>
        /// <param name="summary"></param>
        /// <param name="desc"></param>
        /// <param name="pri"></param>
        /// <param name="stat"></param>
        public virtual void sendticket(string space, string user, string pw, string summary, string desc, Priority pri, TicketStatus stat)
        {
            string ticket_json = "{\"todo\": \"collect all fields here if requires.. see GotMessage() for example...\"}";
            send_event(MimeType.send_ticket, "ticket", ticket_json);

            if (SendTicketEvent != null)
                SendTicketEvent(space, user, pw, summary, desc, pri, stat);
        }
        /// <summary>
        /// send ticket with default priority and status
        /// </summary>
        /// <param name="space"></param>
        /// <param name="user"></param>
        /// <param name="pw"></param>
        /// <param name="summary"></param>
        /// <param name="desc"></param>
        public virtual void sendticket(string space, string user, string pw, string summary, string desc)
        {
            string ticket_json = "{\"todo\": \"collect all fields here if requires.. see GotMessage() for example...\"}";
            send_event(MimeType.send_ticket, "ticket", ticket_json);
            sendticket(space, user, pw, summary, desc, Priority.Normal, TicketStatus.New);
        }
        /// <summary>
        /// send ticket with default priority and status
        /// </summary>
        /// <param name="space"></param>
        /// <param name="user"></param>
        /// <param name="pw"></param>
        /// <param name="summary"></param>
        /// <param name="desc"></param>
        public virtual void T(string space, string user, string pw, string summary, string desc)
        {
            sendticket(space, user, pw, summary, desc);
        }

        /// <summary>
        /// sends a message
        /// </summary>
        /// <param name="type"></param>
        /// <param name="id"></param>
        /// <param name="data"></param>
        public virtual void sendmessage(MessageTypes type, long msgid, string request, string response) 
        {
            string msg_json = "{\"type\": \"" + type.ToString("f") + "\", \"msgid\": " + msgid.ToString() + ", \"request\": \"" + request + "\",  \"response\": \"" + response + "\"}";
            send_event(MimeType.send_message, "message", msg_json);

            if (SendMessageEvent != null) SendMessageEvent(type, (long)ID, 0, msgid, request, ref response); 
        }
        public virtual void sendmessage(MessageTypes type, string data) 
        {
            string msg_json = "{\"type\": \"" + type.ToString("f") + "\", \"data\": \"" + data + "\"}";
            send_event(MimeType.send_message, "message", msg_json);
            
            sendmessage(type, 0, data, string.Empty); 
        }
        /// <summary>
        /// sends a debug message about what your response is doing at the moment.
        /// </summary>
        /// <param name="msg"></param>
        public virtual void senddebug(string msg)
        {
            send_event(MimeType.send_debug, "msg", msg);
            
            if (SendDebugEvent != null) SendDebugEvent(msg); 
        }
        
        /// <summary>
        /// clears the chart
        /// </summary>
        public virtual void sendchartlabel() { sendchartlabel(-1, 0, System.Drawing.Color.White); }
        /// <summary>
        /// draws a label with default color (violet)
        /// </summary>
        /// <param name="price"></param>
        /// <param name="time"></param>
        /// <param name="text"></param>
        public virtual void sendchartlabel(decimal price, int time, string text) { if (SendChartLabelEvent != null) SendChartLabelEvent(price, time, text, System.Drawing.Color.Purple); }
        /// <summary>
        /// draws text directly on a point on chart
        /// </summary>
        /// <param name="price"></param>
        /// <param name="time"></param>
        /// <param name="text"></param>
        public virtual void sendchartlabel(decimal price, int time, string text, System.Drawing.Color c) { if (SendChartLabelEvent != null) SendChartLabelEvent(price, time, text, c); }
        /// <summary>
        /// draws line with default color (orage)
        /// </summary>
        /// <param name="price"></param>
        /// <param name="time"></param>
        public virtual void sendchartlabel(decimal price, int time) { sendchartlabel(price, time, null, System.Drawing.Color.Orange); }
        /// <summary>
        /// draws a line between this and previous point drawn
        /// </summary>
        /// <param name="price"></param>
        /// <param name="time"></param>
        public virtual void sendchartlabel(decimal price, int time, System.Drawing.Color c) { sendchartlabel(price, time, null, c); }
        /// <summary>
        /// same as sendchartlabel
        /// </summary>
        public virtual void CL() { sendchartlabel(); }
        /// <summary>
        /// same as sendchartlabel
        /// </summary>
        /// <param name="price"></param>
        /// <param name="time"></param>
        public virtual void CL(decimal price, int time, System.Drawing.Color c) { sendchartlabel(price, time, c); }
        /// <summary>
        /// same as sendchartlabel
        /// </summary>
        /// <param name="price"></param>
        /// <param name="time"></param>
        /// <param name="text"></param>
        public virtual void CL(decimal price, int time, string text, System.Drawing.Color c) { sendchartlabel(price, time, text, c); }

        /// <summary>
        /// called when a position update is received (usually only when the response is initially loaded)
        /// </summary>
        /// <param name="p"></param>
        public virtual void GotPosition(Position p) 
        {
            send_event(MimeType.got_position, "position", p.ToJson());
        }

        string[] _inds = new string[0];
        string _name = "";
        string _full = "";
        bool _valid = true;
        int _id = UNKNOWNRESPONSE;
        public const int UNKNOWNRESPONSE = int.MaxValue;
        /// <summary>
        /// numeric tag for this response used by programs that load responses
        /// </summary>
        public int ID { get { return _id; } set { _id = value; } }

        /// <summary>
        /// Whether response can be used or not
        /// </summary>
        public bool isValid { get { return _valid; } set { _valid = value; } }
        /// <summary>
        /// Names of the indicators used by your response.
        /// Length must correspond to actual indicator values send with SendIndicators event
        /// </summary>
        public string[] Indicators { get { return _inds; } set { _inds = value; } }
        /// <summary>
        /// Custom name of response set by you
        /// </summary>
        public string Name { get { return _name; } set { _name = value; } }
        /// <summary>
        /// Full name of this response set by programs (includes namespace)
        /// </summary>
        public string FullName { get { return _full; } set { _full = value; } }
        public event DebugDelegate SendDebugEvent;
        public event OrderSourceDelegate SendOrderEvent;
        public event LongSourceDelegate SendCancelEvent;
        public event ResponseStringDel SendIndicatorsEvent;
        public event MessageDelegate SendMessageEvent;
        public event BasketDelegate SendBasketEvent;
        public event ChartLabelDelegate SendChartLabelEvent;



        // helper stuff

        /// <summary>
        /// shutdown a response entirely, flat all positions and notify user
        /// </summary>
        /// <param name="_pt"></param>
        /// <param name="gt"></param>
        public void shutdown(PositionTracker _pt, GenericTrackerI gt)
        {
            send_event(MimeType.shutdown, "foo", "boo".ToJson());

            if (!isValid) return;
            D("ShutdownTime");
            isValid = false;
            bool ShutdownFlat = _pt != null;
            bool usegt = gt != null;
            if (ShutdownFlat)
            {

                D("flatting positions at shutdown.");
                foreach (Position p in _pt)
                {
                    if (usegt && (gt.getindex(p.symbol) < 0)) continue;
                    Order o = new MarketOrderFlat(p);
                    D("flat order: " + o.ToString());
                    sendorder(o);
                }
            }
        }
        /// <summary>
        /// flat a symbol and flag it to prevent it from trading in future
        /// </summary>
        /// <param name="sym"></param>
        /// <param name="activesym"></param>
        /// <param name="_pt"></param>
        /// <param name="sendorder"></param>
        public static void shutdown(string sym, GenericTracker<bool> activesym, PositionTracker _pt, OrderDelegate sendorder) 
        {
            //send_event(MimeType.shutdown, "foo", "boo".ToJson());
            shutdown(sym, activesym, _pt, sendorder, null, string.Empty); 
        }
        /// <summary>
        /// flat a symbol and flag it to allow prevention of future trading with status
        /// </summary>
        /// <param name="sym"></param>
        /// <param name="activesym"></param>
        /// <param name="_pt"></param>
        /// <param name="sendorder"></param>
        /// <param name="D"></param>
        public static void shutdown(string sym, GenericTracker<bool> activesym, PositionTracker _pt, OrderDelegate sendorder, DebugDelegate D) 
        {
            //send_event(MimeType.shutdown, "foo", "boo".ToJson());
            shutdown(sym, activesym, _pt, sendorder, D, string.Empty);
        }
        /// <summary>
        /// flat a symbol and flag it to allow prevention of future trading with status and supplied reason
        /// </summary>
        /// <param name="sym"></param>
        /// <param name="activesym"></param>
        /// <param name="_pt"></param>
        /// <param name="sendorder"></param>
        /// <param name="D"></param>
        /// <param name="reason"></param>
        public static void shutdown(string sym, GenericTracker<bool> activesym, PositionTracker _pt, OrderDelegate sendorder, DebugDelegate D, string reason)
        {
            //send_event(MimeType.shutdown, "foo", "boo".ToJson());

            if (!activesym[sym]) return;
            Order o = new MarketOrderFlat(_pt[sym]);
            if (D != null)
            {
                string r = reason == string.Empty ? string.Empty : " (" + reason + ")";
                D("symbol shutdown" + r + ", flat order: " + o.ToString());
            }
            sendorder(o);
            activesym[sym] = false;
        }
    }
}
