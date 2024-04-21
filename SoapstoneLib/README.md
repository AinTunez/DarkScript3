# SoapstoneLib

SoapstoneLib is a library for FromSoft editor programs to communicate with each other via localhost RPCs. It allows invoking parts of a "server" editor from "client" editors, like clicking on an entity ID in a script editor program and jumping to that entity in a separate map editor program. As part of this, basic metadata about game objects and overall project setup info can also be communicated. In the future, RPC functionality can be expanded for whatever runtime communication editors may need, like multi-step import/export flows.

This is not meant to be a universal solution for editor interoperability. If a program can be invoked on command line or incorporated as a library, that may be preferable. Likewise, editors can share game files, or even custom metadata files, and concurrently watch for modifications to those files.

`project.json` files are one possible standard for editors to share info about projects and the game directories they use. It is supported by Soapstone: you can connect to another program and get all of its currently open projects and their `project.json` paths. It is not required for servers or clients to support it, and other similar standards may be supported.

**Do not use Soapstone functionality unless your project is fully source-available and you'd be able to accept contributions from soulsmods contributors.** Depending on future use cases, it may be necessary to make backwards-incompatible changes to clients and servers, and this is not possible unless all existing usages can be viewed and migrated.

As of 2022, this library is maintained by thefifthmatt. Please get in touch if you have any possible use cases which could be supported.

## Using the library

Editors can set up a server via `SoapstoneServer.RunAsync`, which starts up a Kestrel-based gRPC server, and they can create a client using `SoapstoneClient.GetProvider`. Both of these take a `KnownServer` object which has well-known ports together with a server process name for netstat-based lookup. Clients can use these to seamlessly adapt to servers going up or down, corresponding to editors opening or closing.

The RPC service itself does not hardcode any game object types, so any two programs can communicate any type of object, so long as they agree on a key format. The `SoulsKey` file defines some of these key formats using wrapper C# classes. The intention is for more key classes to be added as more game objects are supported. It is currently a closed set. `SoulsObject` allows representing individual instances of objects with a flat set of key-value properties.

FMGs are a special case. The standard key format uses two custom enums, `FmgType` and `FmgLanguage`. This is because there is no unique key for FMGs which is applicable to all games, so `SoulsFmg` provides ways to look up custom enums from in-game keys, like file names and binder ids, and vice versa.

It is assumed that editors consist of *resources*, which in turn contain *game objects*. In general terms, a resource is a file (or set of files) which can be opened in the editor. Different map files may be different resources if they can be loaded and unloaded independently, but item and menu FMGs may be part of the same resource if they are always loaded together. Some types of game objects may be present in multiple resources at once. For instance, a character model could be loaded as a chrbnd file, but a list of human-readable character model names could be available separately even when no models are loaded.

## Supporting other programming languages

Soapstone can support any language supported by gRPC, not just C#. This includes C++, Go, Java, Python, and more.

Wrapper libraries are not strictly needed to use Soapstone, but creating a library with shared key formats and FMG data is recommended. Please add these as separate subdirectories in this repository.

## Supporting new servers

The initial planned server for Soapstone is DSMapStudio. However, any editor which is authoritative for some set of formats can be a server. If you do this, add a field in `KnownServer` with the next port number, and add any `EditorResourceType`s and key formats necessary to represent desired interactions.

It's not unreasonable for an editor program to be both a server and a client, if it is authoritative for one format and contains secondary references to a different format. Separately, bidirectional streaming RPCs may be possible in the future, which would be preferable for cases like listening to server changes from a client.

## Supporting new clients

You can use this library in editors for runtime connection features, but make sure you're in contact with the server maintainer first so they can support whatever features you need, and can adapt to changes in those features over time. As above, only do this if your program is source-available.
