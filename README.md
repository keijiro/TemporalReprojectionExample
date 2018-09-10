Temporal Reprojection Example
=============================

This example shows how to use the builtin motion vectors to implement temporal
reprojection in Unity.

![gif](https://i.imgur.com/nBG7fe6.gif)

It renders the scene with one second interval and extrapolate in-between frames
with temporal reprojection. It fills low confidence areas with red color, so
basically red indicates ares where reprojection doesn't work.
