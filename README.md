tradelink.afterlife
===================

tradelink.afterlife is my personal attempt to keep TradeLink (including ASP, Kadina etc. excluding &ldquo;lovely&rdquo; Glean) alive!
Please read _documentation/index.html for more info.

Updates on some recent changes:

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
