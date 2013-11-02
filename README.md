tradelink.afterlife
===================

tradelink.afterlife is an attempt to keep TradeLink (including ASP, Kadina etc. excluding lovely Glean) alive!


------------------------------------------------------------------------
02-Nov-2013

It seems I managed to rebuild C++ TWSServer (it is part of tradeling - IB connector) to use latest IB API.
It is not tested yet, since IB has maintenance day today and TWS can not even start, but it is compilable and runable :)

https://github.com/shevkoplyas/tradelink.afterlife

tradelink's responses in published version can take parameters from external JSON files, also if you use "MessageBusableResponseTemplate" class
like in this file "Responses/_TS_step_by_step.cs":

    public class _TS_step_by_step : MessageBusableResponseTemplate

then your response will also emit all the events to RabbitMQ message bus :)

(just experimenting to be able to use external viewers with tradelInk.afterlife, since it does not have viewers, I'd say at all:)

I'll put more info in some readme's later when things are more stable/tested. (don't have much free time). For now the whole thing is just my "as is" take on TL.
------------------------------------------------------------------------
