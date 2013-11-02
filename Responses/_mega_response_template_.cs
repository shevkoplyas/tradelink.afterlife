using System;
using TradeLink.Common;
using TradeLink.API;
using System.ComponentModel;

namespace Responses
{
    /// <summary>
    /// This response is here just as an example of good starting point.
    /// 
    /// Naming Convention:
    ///     track_tick, track_symbols, track_positions, ... - all our trackers use "track_" prefix
    ///     
    /// </summary>
    public class MegaResponseTemplate : ResponseTemplate
    {
        // constructor always goes 1st
        public MegaResponseTemplate() : base() 
        {
            // Custom name of response set by you
            Name = "MegaResponseTemplate";

            // track_symbols_NewTxt() called when new text label is added
            track_symbols.NewTxt += new TextIdxDelegate(track_symbols_NewTxt);

            //     Names of the indicators used by your response.  Length must correspond to
            //     actual indicator values send with SendIndicators event
            Indicators = GenericTracker.GetIndicatorNames(gens());

            track_barlists.GotNewBar += new SymBarIntervalDelegate(GotNewBar);
        }

        //     Call this to reset your response parameters.  You might need to reset groups
        //     of indicators or internal counters.  eg : MovingAverage = 0;
        //
        // dimon: Reset() is executed each time we load our response to kadina and such.
        public override void Reset()
        {
            track_messages.BLT = track_barlists;
        }

        // after constructor we list all the trackers (standard and generic ones)
        //
        GenericTracker<bool> track_symbols = new GenericTracker<bool>();    // track_symbols is our sort of PRIMARY thing
        PositionTracker track_positions = new PositionTracker();
        TickTracker track_ticks = new TickTracker();
        GenericTracker<bool> track_exitsignals = new GenericTracker<bool>();
        // these 2 came from BarRequestor.cs:
        MessageTracker track_messages = new MessageTracker();
        BarListTracker track_barlists = new BarListTracker();
        TickArchiver tick_archiver = new TickArchiver("C:\\dima\\tradelink\\data.saved-ticks");

        GenericTrackerI[] gens()
        {
            return gt.geninds(track_symbols, track_positions, track_ticks, track_exitsignals);  // track_messages and track_barlists can't go here..
        }

        // Don't forget to add trackers to index automatically into following f-n.
        // Note: some trackers, like 'tick' and 'position' don't need to be here)
        //
        // link all the generic trackers together so we create 
        // proper default values for each whenever we add symbol to one
        void track_symbols_NewTxt(string txt, int idx)
        {
            track_exitsignals.addindex(txt, false);
            // add other trackers here...

            //            in LessonGenericTrackers.cs
            //we index this:
            //       PositionTracker pt = new PositionTracker();

            //, but we don't index this:
            //       BarListTracker blt = new BarListTracker(new BarInterval[] { BarInterval.FiveMin, BarInterval.Minute });

            //how do I know which trackers should/must be indexed and which are not?
        }
        
        void GotNewBar(string symbol, int interval)
        {
            // get current barlist for this symbol+interval
            BarList bl = track_barlists[symbol, interval];
            // get index for symbol
            int idx = track_symbols.getindex(symbol);
            // check for first cross on first interval
            if (interval == (int)BarInterval.Minute)
            {
                // 
                D("(int)BarInterval.Minute interval=" + interval);
                return;
            }
            // check second cross
            if (interval == (int)BarInterval.FiveMin)
            {
                // 
                D("(int)BarInterval.FiveMin interval=" + interval);
                return;
            }
            D("else.. interval=" + interval);
            return;

            // // nice way to notify of current tracker values
            // sendindicators(gt.GetIndicatorValues(idx, gens()));

        }




        // GotTick is called everytime a new quote or trade occurs
        public override void GotTick(TradeLink.API.Tick tick)
        {
            // store tick
            tick_archiver.newTick(tick);
            return; // for now only track/store ticks here...

            // ignore quotes
            if (!tick.isTrade) return;

            // ensure we track this symbol, all the other trackers will be indexed inside track_symbols_NewTxt()
            track_symbols.addindex(tick.symbol,false);

            // track tick
            track_ticks.newTick(tick);

            // another "track tick" :)
            //
            // For now we track tick in 2 different trackers: 
            // TickTracker
            // and
            // BarListTracker (connected with MessageTracker)
            //
            // todo: Read more on topic of TickTracker vs BarListTracker. And eventually get rid of one of them.
            //      TickTracker - seems have more functionality, but
            //      BarListTracker - can build bars with any interval (we can mix diff. bar size in one strategy easily using this!) 
            //
            track_barlists.newTick(tick); // dimon: give any ticks (trades) to this symbol and tracker will create barlists automatically

            // if we don't have enough bars, wait for more ticks
            //if (!track_barlists[tick.symbol].Has(BarsBack)) return;

            // get current position
            Position pos = track_positions[tick.symbol];

            // if we're flat and haven't seen this symbol yet, then...
            if (pos.isFlat && !track_symbols[tick.symbol])
            {
                // strart tracking it (other trackers will be updated accordingly, see track_symbols_NewTxt()
                track_symbols[tick.symbol] = true;
                
                D(tick.symbol+": entering long");
                O(new MarketOrder(tick.symbol, EntrySize));
            }
            else if (!pos.isFlat && !track_exitsignals[tick.symbol])
            {
                // get most recent tick data
                Tick k = track_ticks[tick.symbol];
                // estimate our exit price
                decimal exitprice = UseQuotes
                    ? (k.hasAsk && pos.isLong ? k.ask
                    : (k.hasBid && pos.isShort ? k.bid : 0))
                    : (k.isTrade ? k.trade : 0);
                // assuming we could estimate an exit, see if our exit would hit our target
                if ((exitprice != 0) && (Calc.OpenPT(exitprice, pos) > ProfitTarget))
                {
                    track_exitsignals[tick.symbol] = true;
                    D("hit profit target");
                    O(new MarketOrderFlat(pos));
                }
            }
            // --------------------------------------------
            //
            // this is a grey box that manages exits, so wait until we have a position
            //if (!pt[tick.symbol].isFlat) return;
            //
            //// calculate the MA from closing bars
            //decimal MA = Calc.Avg(Calc.Closes(track_barlists[tick.symbol], BarsBack));

            //// if we're short, a cross is when market moves above MA
            //// if we're long, cross is when market goes below MA
            //bool cross = pt[tick.symbol].isShort ? (tick.trade > MA) : (tick.trade < MA);

            //// if we have a cross, then flat us for the requested size
            //if (cross)
            //    sendorder(new MarketOrderFlat(pt[tick.symbol], exitpercent));

            //// notify gauntlet and kadina about our moving average and cross
            //sendindicators(new string[] { MA.ToString(), cross.ToString() });
            // --------------------------------------------
        }



        public override void GotFill(TradeLink.API.Trade fill)
        {
            // keep track of position
            D(fill.symbol + " fill: " + fill.ToString());
            track_positions.Adjust(fill);

            // ensure fill comes from this response
            int idx = track_symbols.getindex(fill.symbol);  // dimon: imho this is ugly (error prone) method. What if 2 strategies working with same symbol simultaneously? it all will go to hell..
            if (idx < 0) return;
            // reset signals if we're flat (allows re-entry)
            if (track_positions[fill.symbol].isFlat)
            {
                track_symbols[fill.symbol] = false;
                track_exitsignals[fill.symbol] = false;
            }
        }

        public override void GotPosition(TradeLink.API.Position pos)
        {
            // keep track of position
            D(pos.symbol + " pos: " + pos.ToString());
            track_positions.Adjust(pos);
        }

        // this came from BarRequestor.cs example
        public override void GotMessage(MessageTypes type, long source, long dest, long msgid, string request, ref string response)
        {
            if (type == MessageTypes.BARRESPONSE)
                D(response);
            track_messages.GotMessage(type, source, dest, msgid, request, ref response);
        }

        //
        // allows user to control MegaResponseTemplate parameters through graphical pop-up interface
        //
        int _entrysize = 100; // default value
        [Description("size used when entering positions.  Negative numbers would be short entries.")]
        public int EntrySize { get { return _entrysize; } set { _entrysize = value; } }

        decimal _profittarget = .1m;  // default value
        [Description("profit target in dollars when position is exited")]
        public decimal ProfitTarget { get { return _profittarget; } set { _profittarget = value; } }

        bool _usequotes = false;  // default value
        [Description("whether bid/ask is used to determine profitability, otherwise last trade is used")]
        public bool UseQuotes { get { return _usequotes; } set { _usequotes = value; } }
    }
    
    /// <summary>
    /// allows user to control MegaResponseTemplate parameters through graphical pop-up interface
    /// </summary>
    public class MegaResponseTemplate_Parameters : MegaResponseTemplate
    {
        public override void Reset()
        {
            ParamPrompt.Popup(this, true, false);
            base.Reset();
        }
    }
}
