libeventfsharp
==============

Simple Echo Server using libevent with Oars library in F#.

This demo code shows how to use the Oars library (which wraps http://libevent.org). The code sets up a listening socket which accepts new connections and echoes any data read on all connections. The Oars library used has been forked and modified. Best to use the one found here in my github account.

This code can be used to create scalable socket servers of all types. Essentially 1 thread is used for all I/O regardless of the number of socket connections.
