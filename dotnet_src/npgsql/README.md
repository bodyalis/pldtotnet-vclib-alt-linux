# Npgsql for PL/.NET

This fork of Npgsql is a submodule for the [PL/.NET](https://github.com/Brick-Abode/pldotnet) project.

## What is pl/dotnet?

The pl/dotnet project extends PostgreSQL to support functions, stored procedures and `DO` blocks for the dotnet platform, including both C# and F#.

### Where can I get the source code for pl/dotnet?

The official repository for pl/dotnet is [https://github.com/Brick-Abode/pldotnet/](https://github.com/Brick-Abode/pldotnet/).

### Where can I read the documentation for pl/dotnet?

Our project wiki is at [https://github.com/Brick-Abode/pldotnet/wiki](https://github.com/Brick-Abode/pldotnet/wiki).

### Is there a white paper explaining the project?

Yes, you can find the [whitepaper on our wiki](https://github.com/Brick-Abode/pldotnet/wiki/pldotnet:-White-Paper).

## What is Npgsql?

Npgsql is the open source .NET data provider for PostgreSQL. It allows you to connect and interact with PostgreSQL server using .NET.

For the full documentation, please visit [the Npgsql website](https://www.npgsql.org).

## How does pl/dotnet make use of Npgsql?

pl/dotnet embraced Npgsql as our PostgreSQL compatibility layer to provide maximum transparency and ease in migrating code between a database client and the database server.

pl/dotnet uses Npgsql to map PostgreSQL data types to .NET data types.  Our implementation of SPI (database access within the stored procedure) is also based on Npgsql.

pl/dotnet incorporates Npgsql with minor, low-level modifications.  We make use of Npgsql's own regression test suite and are working towards perfect compatibility with it.

We are very grateful to the authors of Npgsql, as their work forms an integral piece of our own project.
