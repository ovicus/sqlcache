SQL Object Cache
================

A new caching mechanism was introduced in .NET 4.0. It extends the previous existing mechanism 
intended only for ASP.NET applications. The core of this new caching mechanism is the abstract class ObjectCache 
in the namespace System.Runtime.Caching. .NET provides a concrete implementation for in-memory caching 
through the MemoryCache class. However the in-memory cache are not suitable for distributed scenarios. 

This project provides a concrete implementation of the .NET Caching API, based on SQL Server, 
that can be used in distributed scenarios.

The SqlObjectCache requires a table that should be created through the provided SQL script.
