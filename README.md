Simple SQL-based Cache for .NET
===============================

.NET 4.0 introduced a new caching API that extends the previous existing API 
intended only for ASP.NET applications. The core of this new caching mechanism is the abstract class ObjectCache 
in the namespace System.Runtime.Caching. .NET provides a concrete implementation for in-memory caching 
through the MemoryCache class. However the in-memory cache is not suitable for some distributed scenarios. 

This project provides a concrete implementation of the .NET Caching API, based on SQL Server, 
suitable for distributed scenarios.

The SqlCache implementation requires a table that should be created through the provided SQL script.

What's new in version 1.1?
--------------------------
1. Custom cache table name supported.
2. Add Flush method to remove expired entries.

To use this library add a reference through Nuget:
https://www.nuget.org/packages/Ovicus.Caching