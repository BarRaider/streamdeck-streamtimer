# Stream Countdown Timer for Elgato Stream Deck

Set a timer on your Stream Deck, and have it shown on your Stream too. Will start flashing in a color of your choice when the time is up.

**Author's website and contact information:** [https://barraider.com](https://barraider.com)

## New in v1.9
- :new: `Shared Id` feature allows you to view and control the same timer from different profiles and keys :fire:
- Alert sound will now stop when pressing the key and resetting the timer

## New in v1.8
- :new: `Date/Time Countdown` action - Let's you countdown to a specific date or time. 🔥
- Added a new `Format` setting which allows you to customize the timer displayed on key (and file). 🔥
- Added support for timer Prefix on keys
- Both File Prefix and Key Prefix support newlines \n
- Now customizable if timer starts counting up or not when timer ends
- Minor bug fixes

## Features
- Countdown is written to file so you can display the remaining time on your stream
- Supports adding a title prefix in the file (such as `Time left: `)
- Timer starts flashing when time is up, you can choose the color of the alert
- Hourglass mode creates a visual representation instead of a numeric timer
- Countdown continues writing to the file even if you move to a different Stream Deck profile

### Download

* [Download plugin](https://github.com/BarRaider/streamdeck-streamtimer/releases/)

## I found a bug, who do I contact?
For support please contact the developer. Contact information is available at https://barraider.com

## I have a feature request, who do I contact?
Please contact the developer. Contact information is available at https://barraider.com

## Dependencies
* Uses StreamDeck-Tools by BarRaider: [![NuGet](https://img.shields.io/nuget/v/streamdeck-tools.svg?style=flat)](https://www.nuget.org/packages/streamdeck-tools)
* Uses [Easy-PI](https://github.com/BarRaider/streamdeck-easypi) by BarRaider - Provides seamless integration with the Stream Deck PI (Property Inspector) 

## Change Log


## New in v1.6
- :new: `Focus Timer` action alternates between work and break intervals to improve your focus and get work done. 
- `Auto Reset` feature allows you to reset the timer automatically after a customizable number of seconds
- Timer will now count up after reaching zero, to show you how much time elapsed.
- `Streamathon` mode now supports increasing OR decreasing time on every keypress

## New in v1.5
- `Pause Image` - Displays a custom image when timer is paused (supported both in Normal and Hourglass mode)
- New option to show time remaining in Hourglass mode
- When pressing the button to stop the flashing, it now stops the sound too
- New visual effect for Hourglass mode: `Image Mode` will show the `Pause Image` slowly vanish as the time runs out

## New in v1.4
- Multi-Action support

## New in v1.3
- SOUND SUPPORT :PogChamp: You can now choose a sound that will be played when the timer ends.  Choosing the playback device allows you to send the sound directly to your stream.
- Improved UI to allow setting the filename using a file picker
- Fixed UI issue showing hours with a trailing zero.

## New in v1.2
- :new2: `Streamathon Mode` - Increase the time left by a customizable interval on every key press. (Long click to reset).
- Hourglass Mode now shows an indication on whether the timer is running or paused on every keypress
- Customizable text to show on stream when the timer ends
- Option to clear the text file (and thus remove from stream) when the timer ends