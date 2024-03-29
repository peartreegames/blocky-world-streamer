## BlockyWorldStreamer
Code from "Creating a 3D Tilemap Editor and World Builder in Unity - Part 4" [YouTube video](https://youtu.be/ZxWkhdcHD-4)

## Installation
Can be installed via the Package Manager > Add Package From Git URL...

This repo has a dependency on the BlockyWorldEditor and Evt.Variables package which *MUST* be installed first. (From my understanding Unity does not allow git urls to be used as dependencies of packages)

`https://github.com/peartreegames/blocky-world-editor.git`
`https://github.com/peartreegames/evt-variables.git`

then the repo can be added

`https://github.com/peartreegames/blocky-world-streamer.git`

## Overview

The Blocky World Streamer has three primary functions

    1. Act as a ParentSetter module for the BlockyWorldEditor by placing GameObjects into a grid of scenes.
    2. Optimize those objects by combining colliders.
    3. Stream those scenes at runtime based on a target position.

The package is *very* specific to my own use case and I'd imagine a lot of work would be needed to use in your own projects. This is simply here as an example and hopefully learning resource.

It is really only suitable for top down games, as it streams scenes based on neighbouring grid positions and not view frustums.

## Changes

I've removed the mesh combining and have instead opted for [Mesh Combine Studio](https://assetstore.unity.com/packages/tools/modeling/mesh-combine-studio-2-101956) on the asset store.
