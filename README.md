tradelink.afterlife
===================

tradelink.afterlife is my personal attempt to keep TradeLink (including ASP, Kadina etc. excluding &ldquo;lovely&rdquo; Glean) alive!
Please read _documentation/index.html for more info.

Updates on some recent changes:

<hr>
18-Dec-2013 Abandoning the project :)

[Q] Hi, Dmitry, I was a ´pasive' follower of tradelink. Did you finally formed another group improving or mantaining the free afterlife tradelink?
[A] 
[The short answer]

No and I'm not trying to. 
James (wilhelmstroods@gmail.com) is trying to put it all back together: creating a web site + group of developers to support TradeLink, but so far I didn't saw any major "new versions" from that direction.


[A bit longer answer]

I would start from the question to everybody:

[Q] what was very good and is holding you to use TradeLink? what kind of functionality does TradeLink provide for you that you can't find anywhere else?

for me the answer was: 
    - gives me IB connector which was woking out of the box
    - calls my callback functions (like "got tick", "got order" etc.)
    - stores TIK files
    - can replay TIKs for my backtest experiments

That is it! 
That is all I used (TA-lib functionality is a separate project, so I didn't mentioned it here)

[Q] what was not good about TradeLink experience?

again my imho here was:
    - no charting at all!
    - works only on windows (I want to move trading bot to Amazon cloud Ubuntu boxes)
    - no more support from PracPlay (this is really big one!)
    - is quite complex solution with very limited  capabilities
    - now way to easily see what is going on during live trading
      (ideally this functionality should also be available remotely)
      (also it should be possible to see real-time stats per each individual response running even if two (or more) responses are trading same ticker)

by the last item I mean: if you look deeper into IB connector, then you'll notice bunch of functionality is not implemented. I'll just list some questions which you CAN'T make to work (without code modification) using existing "latest TradeLink" snapshot:
    - how do resolve ambiguity for some tickers (like MSFT)
    - how do you subscribe to tickers not SMART-routed? like for example forex pairs CAD.JPY should use "IDEALPRO" instead.
    - how do you debug "failed to subscribe" cases for some securities - there's no
      visibility of why it failed and where particular it failed (and you need to be able to fix it yourself)
    - how do you subscribe to historical data? for example you algo-trading bot could get last 3 days 5sec bars to 
    - how do you trade options?
    - how do you get full option chains for some stocks
    - what do you do when IB publish new TWS API (which they do from time to time)?

The root of all events is broker's API (since all the rest is just layers of additional complexities on top of that), so few months ago I joined TWA API yahoo group (I posted detailed info about that group + forwarded few posts here from that group few weeks ago).
Now I have 2 versions of own C++ connectors which is 100 times simpler and it already gives me:
    - IB connector with all functionality at my fingertips
    - calls my callback functions (like "got tick", "got order"...)

As you can see - this is already more than 50% of what I need :)
For now it does not storeTIK files for me, but as you can understand it is very easy to add.
To make "replay" might be more tricky, but also doable.

Also on the good side I have now:
    - decreased complexity (about 1.6K-1.7K lines of cpp code! compare to ~200K lines in cs files of TradeLInk!!) 
    - much less moving parts (no longer invisible windows exchanging serialized structures)
    - posix compatible i.e. I can compile/run the solution in windows, Linux (Ubuntu) and OS-X !

So for now I'm abandoning TradeLink as a whole piece. It was nice toy and it was good milestone on the way to algo-trading,
but it is too limited and heavy without 24/7 support from some team.

Next steps for me would be to:
    - add ZeroMQ messaging into the mix after I'm completely happy with what I've got so far. In this case 
    - add some visualizer
    - add ability to see what's going on remotely

Regards,
Dmitry Sh.

<hr>
15-Nov-2013 

* documentation - added as a folder to store formal description of the project and its parts.
  Eventually this shoud migrate to some wiki engine or alike.

* TWSServer (IB connector) is fully funcitonal. Now it produces much more visibility on what is going on right now.
  TWSServer supports latest IB API:
	IB API Version:	API 9.69
	Release Date: July 1 2012

* 4 rich edit pannels have been added to log separately messages between all moving parts:
	1) IB API --> TWSServer
	2) IB API <-- TWSServer
	3) TWSServer --> Clients
	4) TWSServer <-- Clients

  All pannels can be scrolled syncroniously (when you scroll 1st one:)
  Original implementation with CEdit hit the limit of 30Kbyte so had to be replaced with rich edits.

* Each logged line is timestamped. Since we usually have more than 1 line/sec additional through counter added to timestamp

* All pannels content is mixed together (with proper line prefix) into one log file. Example:
  C:\tradelink.afterlife\_logs\TWSServer\TWSServer.20131113.txt
	* * *
	commn:145348.15 TLServer_WM::needStock ABX
	IB->S:145348.16 SrvGotTick:ABX,20131113,145348,,18.930000,40000,,0.000000,0.000000,0,0,,,0
	S->CL:145348.17 TLServer_WM::SrvGotTick: TICKNOTIFY (a)ABX,20131113,145348,,18.930000,40000,,0.000000,0.000000,0,0,,,0
	IB->S:145348.18 TWS_TLServer::tickSize
	* * *

<hr>
02-Nov-2013

It seems I managed to rebuild C++ TWSServer (it is part of tradeling - IB connector) to use latest IB API.

tradelink's responses in published version can take parameters from external JSON files, also if you use "MessageBusableResponseTemplate" class
like in this file "Responses/_TS_step_by_step.cs":

    public class _TS_step_by_step : MessageBusableResponseTemplate

then your response will also emit all the events to RabbitMQ message bus :)

(just experimenting to be able to use external viewers with tradelink.afterlife, since it does not have viewers, I'd say at all:)
<hr>
