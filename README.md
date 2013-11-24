SQL Cache
=========

.NET 4.0 introduced a new caching mechanism that extends the previous existing mechanism 
intended only for ASP.NET applications. The core of this new caching mechanism is the abstract class ObjectCache 
in the namespace System.Runtime.Caching. .NET provides a concrete implementation for in-memory caching 
through the MemoryCache class. However the in-memory cache is not suitable for some distributed scenarios. 

This project provides a concrete implementation of the .NET Caching API, based on SQL Server, 
that can be suitable for distributed scenarios, not only Web but distributed computing in general.

The SqlCache implementation requires a table that should be created through the provided SQL script.
