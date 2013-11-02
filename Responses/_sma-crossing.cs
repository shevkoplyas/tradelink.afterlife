using System;
using TradeLink.Common;
using TradeLink.API;
using System.ComponentModel;

using System.Collections.Generic;   // for List<type>, Dictionary<type,type> etc. http://social.msdn.microsoft.com/Forums/en/csharpgeneral/thread/30e42195-73ab-4ef4-bc91-62aa89896326

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using TradeLink.AppKit; // for class ResponseParametersHolder

using NDesk.Options; // for cmd-line args parsing


// mongodb: as a minimum:
using MongoDB.Bson;
using MongoDB.Driver;   // http://docs.mongodb.org/ecosystem/tutorial/use-csharp-driver/
// Additionally, you will frequently add one or more of these using statements:
using MongoDB.Driver.Builders;
using MongoDB.Driver.GridFS;
using MongoDB.Driver.Linq;
using MongoDB.Bson.Serialization;
// http://docs.mongodb.org/ecosystem/tutorial/getting-started-with-csharp-driver/#getting-started-with-csharp-driver



// src: http://stackoverflow.com/questions/4764978/the-type-or-namespace-name-could-not-be-found
// quote:
//    Turns out this was a client profiling issue.
//    PrjForm was set to ".Net Framework 4 Client Profile" I changed it to ".Net Framework 4", and now I have a successful build.
//    Thanks everyone! I guess it figures that after all that time spent searching online, I find the solution minutes after posting, I guess the trick is knowing the right question to ask..

namespace Responses
{

    public class _SMA_Crossing_Response : ResponseTemplate      // class name starts with "_" just to make it easy to find in a list of responses...
    {
        static int verbosity;
        static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("Usage: greet [OPTIONS]+ message");
            Console.WriteLine("Greet a list of individuals with an optional message.");
            Console.WriteLine("If no message is specified, a generic greeting is used.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }

        static void Debug(string format, params object[] args)
        {
            if (verbosity > 0)
            {
                Console.Write("# ");
                Console.WriteLine(format, args);
            }
        }

        // dimon: external json files support:
        MongoDB.Bson.BsonDocument bson = null;
        TradeLink.AppKit.ResponseParametersHolder response_parameters_holder = null;

        // constructor always goes 1st
        string debug_message_from_constructor = ""; // need to store it and show on 1st tick, otherwise debug messages are wiped out when ticks start to arrive
        public _SMA_Crossing_Response()
            : base()
        {
            string[] args = MyGlobals.args;

            bool show_help = false;
            string kadina_config = "";
            string response_config = "";
            int repeat = 1;

            var p = new OptionSet() {
            { "k|kadina-config=", "response json configuration file",
              v => kadina_config = v },
            { "r|response-config=", "response json configuration file",
              v => response_config = v },
            { "z|zepeat=", 
                "the number of {TIMES} to repeat the greeting.\n" + 
                    "this must be an integer.",
              (int v) => repeat = v },
            { "v", "increase debug message verbosity",
              v => { if (v != null) ++verbosity; } },
            { "h|help",  "show this message and exit", 
              v => show_help = v != null },
        };

            List<string> extra;
            try
            {
                extra = p.Parse(args);
            }
            catch (OptionException e)
            {
                Console.Write("greet: ");
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `greet --help' for more information.");
                return;
            }

            if (show_help)
            {
                ShowHelp(p);
                return;
            }



            // get settings from json file
            if (response_config != "")
            {
                response_parameters_holder = new ResponseParametersHolder();
                response_parameters_holder.parse_json_file(response_config);
                bson = response_parameters_holder.bson;
                _slow_ma_bar = BsonSerializer.Deserialize<int>(bson["_slow_ma_bar"].ToJson());
                _fast_ma_bar = BsonSerializer.Deserialize<int>(bson["_fast_ma_bar"].ToJson());

                // Custom name of response set by you
                Name = BsonSerializer.Deserialize<string>(bson["name"].ToJson());

                debug_message_from_constructor = "parsed json file - OK (set slow_ma=" + _slow_ma_bar + " fast_ma=" + _fast_ma_bar;
                D(debug_message_from_constructor); // wtf? why this message never showed up? seems messages are cleaned right before 1st GotTick();
            }


            // track_symbols_NewTxt() called when new text label is added
            track_symbols.NewTxt += new TextIdxDelegate(track_symbols_NewTxt);

            //     Names of the indicators used by your response.  Length must correspond to
            //     actual indicator values send with SendIndicators event
            Indicators = GenericTracker.GetIndicatorNames(gens());

            track_barlists.GotNewBar += new SymBarIntervalDelegate(GotNewBar);
        }


        // after constructor we list all the trackers (standard and generic ones)
        //
        GenericTracker<bool> track_symbols = new GenericTracker<bool>("symbol");    // track_symbols is our sort of PRIMARY thing
        PositionTracker track_positions = new PositionTracker("position");
        TickTracker track_ticks = new TickTracker();
        // these 2 came from BarRequestor.cs:
        BarListTracker track_barlists = new BarListTracker(BarInterval.Minute);

        // track our moving averages with "history" in a list of decimao (separate list per each symbol)
        GenericTracker<List<decimal>> track_sma_slow = new GenericTracker<List<decimal>>();
        GenericTracker<List<decimal>> track_sma_fast = new GenericTracker<List<decimal>>();

        // tick archiver will save ticks on a disk (note: ASP does it by default for you already)
        //TickArchiver _ta = new TickArchiver("c:\\dima\\tradelink\\data.saved-ticks");

        // keep track of time for use in other functions
        int time = 0;

        GenericTrackerI[] gens()
        {
            return gt.geninds(track_symbols, track_positions);
        }

        void track_symbols_NewTxt(string txt, int idx)
        {
            // index all the trackers we're using
            track_positions.addindex(txt);
            track_sma_slow.addindex(txt, new List<decimal>());
            track_sma_fast.addindex(txt, new List<decimal>());
        }

        void GotNewBar(string symbol, int interval)
        {
            //if (symbol.ToUpper() != "ABX") return;

            // get current barlist for this symbol+interval
            BarList bl = track_barlists[symbol, interval];

            // get index for symbol
            int idx = track_symbols.getindex(symbol);

            string dbg_msg = "GotNewBar(" + symbol + ", " + interval + "):";

            // check for first cross on first interval
            if (interval == (int)BarInterval.Minute)
            {
                decimal no_tracker_slow_ma = Calc.Avg(Calc.EndSlice(bl.Close(), _slow_ma_bar));
                decimal no_tracker_fast_ma = Calc.Avg(Calc.EndSlice(bl.Close(), _fast_ma_bar));

                track_sma_slow[symbol].Add(no_tracker_slow_ma);
                track_sma_fast[symbol].Add(no_tracker_fast_ma);

                // drawings...
                if (bl.Close().Length > 1)
                {
                    // this is how we draw line, which connects all bars close.
                    //decimal val = bl.Close()[bl.Close().Length - 2]; // length-1 would be last, so length-2 is our previus bar
                    //int time_prev = bl.Time()[bl.Time().Length - 2];
                    //sendchartlabel(val, time_prev, System.Drawing.Color.Green);

                    // draw 2 sma lines:
                    sendchartlabel(no_tracker_slow_ma, time, System.Drawing.Color.Blue);
                    sendchartlabel(no_tracker_fast_ma, time, System.Drawing.Color.FromArgb(0xff, 0x01, 0x01)); // wtf? why red line multiplies after each sell order?!

                    //sendchartlabel(bl.Close()[bl.Close().Length - 2],
                    //    time,
                    //    (time - 60).ToString(),
                    //    System.Drawing.Color.Green);

                    // do the trade (if required)
                    decimal[] sma_slow = track_sma_slow[symbol].ToArray();
                    decimal[] sma_fast = track_sma_fast[symbol].ToArray();

                    decimal prev_sma_slow = sma_slow[sma_slow.Length - 2];
                    decimal prev_sma_fast = sma_fast[sma_fast.Length - 2];

                    decimal curr_sma_slow = sma_slow[sma_slow.Length - 1]; // imho quite ugly..
                    decimal curr_sma_fast = sma_fast[sma_fast.Length - 1]; // todo: read more on how to work with Lists

                    // sma just crossed?
                    bool should_buy = prev_sma_slow > prev_sma_fast && curr_sma_slow < curr_sma_fast;
                    bool should_sell = prev_sma_slow < prev_sma_fast && curr_sma_slow > curr_sma_fast;

                    dbg_msg += " slow=" + curr_sma_slow.ToString("000.000");
                    dbg_msg += " fast=" + curr_sma_fast.ToString("000.000");
                    dbg_msg += " pr_slow=" + prev_sma_slow.ToString("000.000");
                    dbg_msg += " pr_fast=" + prev_sma_fast.ToString("000.000");
                    dbg_msg += " [" + symbol + "].isFlat=" + track_positions[symbol].isFlat.ToString();
                    dbg_msg += " track_positions[symbol].ToString=" + track_positions[symbol].ToString();
                    dbg_msg += " should_buy=" + should_buy.ToString();
                    dbg_msg += " should_sell=" + should_sell.ToString();

                    //senddebug("GotNewBar(): " + debug_position_tracker(symbol));

                    if (should_buy)
                    {
                        // come buy some! (c) Duke Nukem
                        sendorder(new BuyMarket(symbol, EntrySize));
                        dbg_msg += " BuyMarket(" + symbol + ", " + EntrySize.ToString() + ")";
                    }

                    if (!track_positions[symbol].isFlat && should_sell) // we don't short, so also check if !flat
                    //if ( should_sell) // we don't short, so also check if !flat
                    {
                        sendorder(new SellMarket(symbol, EntrySize));
                        dbg_msg += " SellMarket(" + symbol + ", " + EntrySize.ToString() + ")";
                    }
                }
            }
            //else
            //{
            //    // 
            //    dbg_msg += "GotNewBar() other interval=" + interval;
            //}

            // spit out one dbg message line per bar
            //senddebug(dbg_msg);

            // nice way to notify of current tracker values
            sendindicators(gt.GetIndicatorValues(idx, gens()));
            return;
        }

        // GotTick is called everytime a new quote or trade occurs
        public override void GotTick(TradeLink.API.Tick tick)
        {

            // tmp workaround for "how to show debug messages from response constructor"
            if (debug_message_from_constructor.Length > 0)
            {
                D(debug_message_from_constructor);
                debug_message_from_constructor = "";
            }

            // keep track of time from tick
            time = tick.time;

            // safe tick to files
            //_ta.newTick(tick);

            // ignore quotes
            if (!tick.isTrade) return;

            // ignore ticks with timestamp prior to 9:30:00am
            if (tick.time < 93000) return;

            // ensure we track this symbol, all the other trackers will be indexed inside track_symbols_NewTxt()
            track_symbols.addindex(tick.symbol, false);

            // track tick
            track_ticks.newTick(tick);
            track_barlists.newTick(tick); // dimon: give any ticks (trades) to this symbol and tracker will create barlists automatically

            // if we don't have enough bars, wait for more ticks
            if (!track_barlists[tick.symbol].Has(_slow_ma_bar)) return;

        }




        void shutdown()
        {
            D("shutting down everything");
            foreach (Position p in track_positions)
                sendorder(new MarketOrderFlat(p));
            isValid = false;
        }

        void shutdown(string sym)
        {
            // notify
            D("shutting down " + sym);
            // send flat order
            sendorder(new MarketOrderFlat(track_positions[sym]));
            // set inactive
            //_active[sym] = false;
        }

        public override void GotFill(Trade fill)
        {
            // make sure every fill is tracked against a position
            track_positions.Adjust(fill);

            // chart fills
            sendchartlabel(fill.xprice, time, TradeImpl.ToChartLabel(fill), fill.side ? System.Drawing.Color.Green : System.Drawing.Color.Red);

            senddebug("GotFill(): sym: " + fill.symbol + " size:" + fill.xsize + " price: " + fill.xprice + " time: " + fill.xtime + " side: " + fill.side + " id: " + fill.id);

            // get index for this symbol
            //int idx = _wait.getindex(fill.symbol);
            // ignore unknown symbols
            //if (idx < 0) return;
            // stop waiting
            //_wait[fill.symbol] = false;
        }

        public override void GotPosition(Position p)
        {
            // make sure every position set at strategy startup is tracked
            track_positions.Adjust(p);

            // do some logging
            string dbg_msg = "";
            dbg_msg += "GotPosition(): sym: " + p.symbol + " size:" + p.Size + " avg price: " + p.AvgPrice;
            senddebug("GotPosition(): " + debug_position_tracker(p.symbol));

            senddebug(dbg_msg);
        }

        public string debug_position_tracker(string sym)
        {
            string dbg_msg = "";
            dbg_msg += " track_positions[symbol].ToString=" + track_positions[sym].ToString();
            dbg_msg += " t.size=" + track_positions[sym].Size;
            dbg_msg += " t.isFlat=" + track_positions[sym].isFlat;
            dbg_msg += " t.isLong=" + track_positions[sym].isLong;
            dbg_msg += " t.isShort=" + track_positions[sym].isShort;
            dbg_msg += " t.FlatSize=" + track_positions[sym].FlatSize;
            return dbg_msg;
        }

        //
        // allows user to control _SMA_Crossing_Response parameters through graphical pop-up interface
        //
        int _entrysize = 100; // default value
        [Description("size used when entering positions.  Negative numbers would be short entries.")]
        public int EntrySize { get { return _entrysize; } set { _entrysize = value; } }

        int _slow_ma_bar = 12; // default value
        [Description("Slow MA size in bars.")]
        public int SlowMABars { get { return _slow_ma_bar; } set { _slow_ma_bar = value; } }

        int _fast_ma_bar = 3; // default value
        [Description("Fast MA size in bars.")]
        public int FastMABars { get { return _fast_ma_bar; } set { _fast_ma_bar = value; } }

        decimal _profittarget = .1m;  // default value
        [Description("profit target in dollars when position is exited")]
        public decimal ProfitTarget { get { return _profittarget; } set { _profittarget = value; } }

        bool _usequotes = false;  // default value
        [Description("whether bid/ask is used to determine profitability, otherwise last trade is used")]
        public bool UseQuotes { get { return _usequotes; } set { _usequotes = value; } }
    }

}
