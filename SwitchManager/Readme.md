# Switch Manager

Switch Manager is a Nintendo Switch rom manager and CDN downloader in one. It lists every existing switch game that
you are able to provide a title key for, owned or unowned. You can filter the list in multiple ways, create a download queue,
download unowned roms from the CDN, scan your disk for existing roms and more.

Future updates may add emulator integration, when Switch emulators are good enough for commercial games.

### Prerequisites

This project uses .NET Framework 4.7.2, the latest at the time I started the project. Nothing earlier will suffice, as there are some specific networking-related functions
(I can't remember what they are) that only work on 4.7.2. It was developed on Visual Studio Community 2017 using Windows 10
Creator's Update, and I can't
guarantee successful compilation on any earlier version of Visual Studio, .NET Framework or Windows.

Otherwise, compiling should be as simple as loading up the solution and hitting build. The required NuGet libraries
should be included as part of the solution. 

### Installing

As said above, load the Solution in Visual Studio 2017. Build, then run. I haven't created a fresh project myself, so
I have no idea if there are any other weird requirements to get it running.

## Deployment

Build with the Release profile and copy the /bin/Release contents elsewhere. This is a portable app and requires no
installation, though there are several files that are absolutely necessary to proper functioning and should be included in
the application's Working Directory. Make sure to include blank.jpg in the WD. 

CDN downloading requires a working switch certificate. Please don't include a certificate when distributing to others. It is
almost certainly illegal, and a good way to have that cert banned very quickly, ESPECIALLY if it is your personal certificate.
It is the user's responsibility to provide eshop.pfx (there is no in-app facilty to convert shopN.pem to a PFX file) and
a client certificate in either PEM or PFX form, which can be imported via the Import New Credentials menu item.

NOTE: eShop data access is not implemented, and there is no timetable for its inclusion.

Hactool is necessary for proper functioning. The default location for it is in {applicationDirectory}/hactool/*. I'm not sure 
about the ethics or legality of including hactool with a release, but it is probably fine. The user will need to get their own
switch keys (hactool/keys.txt by default) since it isn't legal to distribute. Keys.txt is necessary for functionality.

Nothing in the Images folder is necessary for functioning. The app will auto download any images into the image cache as necessary. 
However, you probably could release this with a full image cache to save the users some CDN requests to get them. I'm not
sure about the copyright implications of this. Cached images are not included in the project repo. 

## Getting Started

If all required files are in place (they should be if the app was deployed properly and users have supplied valid certs and
keys.txt), the app will launch with an empty game list.

# Getting Title Keys

The first step is to import title keys. Title keys use the typical format:

IIIIIIIIIIIIIIII|KKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKK|Name of Title Here

I is the title ID and is 16 hex characters long, and is the only required part. Sometimes the title id you get will 
be 32 characters - this is the rights ID, and is just the 16 character title ID, 15 0s and the Key Generation. If the
rights ID is provided, the app will snip the first 16 characters automatically, so don't worry about it.

K is the title key. If the title key is provided, it will be associated with the title. The title will be packable into an NSP
and playable on a live Switch. If the title key is missing, the title will still be added to your library with the state of "Preloadable".
Such a title can still be downloaded, but it CANNOT be packed into an NSP. When downloaded, the status will become "Preloaded"
instead of Owned, the rom path will be a directory instead of an NSP file and the game will be unplayable without adding a title key.

To make a Downloaded title playable, thus making it Owned and packing it to an NSP, you need only paste the new title key in
using the Tools -> Paste Title Keys command, this time with a valid Key and the same ID that was used before (Name is optional).
Download the game again. All files already exist, so the necessary ticket and certificate will be generated using the new title key,
the NSP will be repacked (if NspRepack is true) and status will become Owned. That's it - just press "Download" again.

Name is optional, but it is pretty much always included in any titles listing. It is ONLY used for display purposes. If you
intentionally blank out all titles and subsequently load a game info JSON (or get game data from the eshop), 
all of your titles will be given their "official" names. When packing an NSP or otherwise downloading game files, the app will try to get the
official Name and Publisher from within the game files.

Loading title keys can be done in three ways:

Tools -> Update Title Keys will download the keys from App.config["TitleKeysURL"] and add any new keys to your list.
Any titles that already exist will be ignored, unless the title is missing a Key or a Name (then they will be updated from
the new downloaded titles). The new titles will be displayed in a scrollable popup and all new titles will be marked with the
status "New". You can view only new titles by checking the "New" filter.

Tools -> Load Title Keys does the same thing as Update Title Keys, but the user can load them from a file on disk instead
of via the web. If you got a copy of titlekeys.txt, or one was provided by the deployer of this app, you can start out
by using Load Title Keys. If the web copy of the titles is outdated or down but an updated version is available, use this option.

Tools -> Paste Title Keys allows you to paste the new title entries directly into a text box and import them without
putting them in a file first. This is useful if you have a source of frequently updating title IDs/keys that is newer
than your TitleKeysURL source.

All three methods have the same results - only new titles are added, and they are displayed in a list and marked as New or Missing Title Key.
Titles will remain New until you either download them (then they are marked as Downloaded or Owned) or manually mark them as Unowned in the UI.
Make sure to mark titles as "Unowned" after you've review them.

When initially loading up your library, everything will be new. Use Library -> Clear New Items to change ALL titles that are New
to be Unowned instead. Title Key Missing will be unchanged.

Once title keys are loaded, there are some optional steps you can take for an optimal start.

# Loading game information, or someone else's library file

First, if you have a Game_info.json (from CDNSP or elsewhere) or a library.xml (that came with the app or some other source),
go to Tools -> Import Game Info. Select the file (JSON or XML) and confirm to load everything from it. Loading a library XML
file is a valid alternative to loading a title keys file, and is the fastest way to get up and running. See notes about this later.

Game_info.json contains metadata about every title - release date, description, name, price, etc. Importing it
adds all of that information into your library. You can delete or ignore the JSON file after importing. After importing,
your games will display descriptions, box art from Nintendo's servers (obviating the need to download the images into the cache),
a huge benefit if you want to minimize CDN requests), publisher, release date, etc.

At this time, dynamic title data loading is NOT implemented, but is a work in progress. Loading from a JSON or XML file
is necessary for title metadata to appear until the feature is implemented. Since the JSON file is static, you will only have
data for games that are included in the JSON file. Newer games will be missing info.

Notes about data import:
	Loading an XML that is from another person's library will load EVERYTHING, including rom paths and rom status, the path to
	the icon file in cache (which might not exist on your system) and favorites information. Results could be... unpredictable.
	If loading some else's library.xml, make sure to remove all references to the following XML entries:
	
	IsFavorite
	Path
	Icon
	State

	Loading an XML from another person's library will ALSO (unless this data is removed) load all of the title IDs and Keys
	from their library. This is an alternative to loading titlekeys.txt or updating title keys, as long as you're careful
	to sanitize the personal info. I may add an export game info button that turns library.xml into something you can
	distribute for use on other people's systems.

	Loading game info does NOT replace the existing name unless it is blank. I've found that the "official" title names
	can be strange. For example, demos often have the same name as the main game (leaving no way to distinguish them except
	by box art) and games from different regions have the same name (and often the same art), making it impossible to
	tell which is EU and which is US, for example.

	If your data import contains boxart, all of your icons will be permanently replaced with box art. This is a good thing if
	you don't want to make hundreds of cdn requests just to download the NCA files containing the game icons, nor save a big
	cache full of images, but if you would rather have your images cached, or if you like the look of the cached icons better,
	there's nothing to be done except go into your library and delete all instances of both BoxArtUrl and Icon. This will
	force all icons to be loaded from scratch, either from your cache or directly from the title files (requires CDN request);

# Scanning for roms

To find titles on your system, go to Library -> Scan For Games. This currently ONLY works on NSPs (not unpacked directories full of game files).

This will find every rom in a directory of your choice and match it with an existing title in your library. The file name
must match the CDNSP naming convention:

For games: Game Name [titleid][v0].nsp
For updates: Game Name [UPD][updateid][version].nsp 
For dlc: [DLC] Game Name [dlcid][version].nsp

titleid is the ID of the base game. Self-explanatory.

updateid is the same as titleid, with the last 000 replaced with 800.

dlcid is the ID of the DLC (should be included in your titlekeys download). 
The ID for DLC is the same as the base game's ID, but with last 4 characters replaced with [XDDD], 
where X is one more than the same digit in the base game ID and DDD is 001, 002, 003, etc.
For example, the DLC IDs for a game with ID 01002fc00412c000 would be 01002fc00412d001, 01002fc00412d002, 01002fc00412d003, ..., since c + 1 = d in hex.

version always starts with a v, followed by the version number, which is always 0 for games, and always a multiple of 65536, which is 0x10000 in hex, for updates and DLC.
It is impossible to download older versions of DLC. I've tried downloading DLC of version 0 and it fails if there is a version greater than 0.

In other words, the versions work like this
'''
Update #1 -> 0x10000 = 65536 in decimal
Update #2 -> 0x20000 = 131072 in decimal
Update #3 -> 0x30000 = 196608 in decimal
Update #4 -> 0x40000 = 262144 in decimal
'''
You can see how the version system makes much more sense in hex. However, CDNSP started the naming convention in decimal, unfortunately, so I'm following it,
and the CDN expects versions in decimal format as well.

For each rom found on your system whose ID matches an existing title, the Rom Path of the title is set to the path of the rom file,
the State is set to Owned and the file size is calculated by the file on disk.

Side note: If, for whatever reason, the names of your demos don't contain the word demo, but the file names of your roms DO contain demo,
scanning will correctly mark those titles as Demos in your library by appending the word Demo to their names. Titles will
ONLY be marked as a demo if they contain demo, trial ver or special trial in their names.

## Usage

# Browsing

All titles are listed in a DataGrid, which is really just a details-view list. By default, all games are shown, owned or unowned,
and they are sorted by title name. Clicking on the row for a game "expands" the row to give more detailed information, an icon,
and download links.

You can switch the highlighted title using the arrow keys. You can also select individual cells with the keyboard. If they are editable,
you can edit them via keyboard after you highlight them with a dashed line using the arrow keys. Favorite can be toggled with the space bar, for example.

# Editing

Favorite can be checked to mark a title as a favorite. This lets you filter the library so you only see your favorite titles. See the Filtering section.

State is a drop-down list containing all possible "states" a title can be in. You can change any title to any state. States tell
you (and the app) the current status of the title within your library.

	Preloadable - The title is not downloaded and has no title key, so it can't be unlocked for play. Metadata like size and icon will stil appear, though.
	Unowned - Any title that has a title key but hasn't been downloaded, and isn't marked as New. The "default" status.
	New - Signifies that the title was recently added to the library. Any titles added via update/load/paste title keys is marked as New.
	Preloaded - The title's files have been downloaded, but there is no title key so it has not been unlocked for play. The Rom Path is always a directory because these are never packed as NSPs.
	Owned - The title's files have been downloaded, its certificate and ticket have been generated and it is fully unlocked for play. It may or may not be packed into an NSP, so the Rom Path can be a directory or NSP file.
	On Switch - Indicates that the title has been copied to your Switch's SD card. This must currently be manually triggered, but I hope to add functionality to copy files from the library to your Switch SD card and mark them as "On Switch".
	Hidden - The title will not appear in the library list unless the Hidden filter is checked. Good for hiding games from other regions or languages, temporary betas or any titles you know you will never want to download.

Rom Path is normally generated automatically after a download or a library scan. Paths can be modified manually by clicking the
path and using the keyboard to edit it. This is useful for when you rename the file on your disk and want the library to reflect that,
or you deleted the NSP file and you want to clear the rom path completely.

Change the title's State to "Hidden" to temporarily "delete" it from your library. You can always see it again by checking "Show Hidden", and you can
even change the state back so it isn't hidden anymore.

You can right click a row and select "Delete Title" to completely remove the title from your library. The only way to add it back
is to paste or add the title key again. I suggest using the Hidden option instead, because any full title key update will restore the title.

Right click a row and select "Remove From Library" to clear the rom path and set the State to Unowned or Title Key Missing. Your rom on disk will NOT
be deleted. The game will be readded if you scan your library again. This is a shortcut for manually editing the State and Rom Path.

# Filtering

The library can be filtered in many different ways by using the search box and the various checkboxes. Upon app start, all games (not DLC or Demos) are displayed, owned or not.

Type into the search box at the top to filter the list to show only titles whose name OR title ID matches what you type. The
list updates every time you add or remove text.

Demos - Check this to allow Demos to appear. A Demo is any title with the word "demo" or "trial ver" or "special trial". Not all demos will be caught, since some may
not have the word demo or the title, and may have an alternative word or be in another language. I currently have no way of detecting that a title is a demo through metadata.
Checking this box does not hide titles that aren't demos, it just allows them to be displayed. Unchecking the box hides all Demos.
This box is by default NOT CHECKED when the app starts, so Demos are not visible without first checking the box.

DLC - Check this to allow DLC to appear. A DLC title is any title detected to be DLC. This is easy, as not only are they usually prefixed with [DLC], their title IDs have a special
format that means any title can be identified as DLC, and you can always find the base title for a piece of DLC.
Checking this box does not hide titles that aren't DLC, it just allows them to be displayed. Unchecking the box hides all DLC.
This box is by default NOT CHECKED when the app starts, so DLC is not visible without first checking the box.

Games - Check this to allow Games to appear. This is self-explanatory, and you'll probably want this checked most of the time.
Checking this box does not hide titles that aren't Games, it just allows them to be displayed. Unchecking the box hides all Games.
This box is by default CHECKED when the app starts, so Games are visible.

Owned - Check this to allow "Owned" titles to appear. This means any title whose State is "Owned" or "On Switch". It does not include "Downloaded" titles. If the title is on your disk
and has a valid rom path, it will still not appear as owned unless the State is set to Owned or On Switch.
Checking this box does not hide titles that are Not Owned, it just allows Owned titles to be displayed. Unchecking the box hides all Owned titles.
This box is by default CHECKED when the app starts, so Owned titles are visible.

Not Owned - Check this to allow "Not Owned" titles to appear. This means any title whose State is anything other than "Owned" or "On Switch". If the NSP is missing from your disk
and/or doesn't have a rom path, it will still not appear as Not Owned unless the State is set to something other than Owned or On Switch.
Checking this box does not hide titles that are Owned, it just allows Not Owned titles to be displayed. Unchecking the box hides all Not Owned titles.
This box is by default CHECKED when the app starts, so Not Owned titles are visible.

Hidden - Check this to allow "Hidden" titles to appear. This means any title whose State is "Hidden". 
Checking this box does not hide titles that are Hidden, it just allows Hidden titles to be displayed. Unchecking the box hides all Hidden titles.
This box is by default NOT CHECKED when the app starts, so Hidden titles are not visible without first checking the box.

Favorites - This works differently from the other filters. When checked, the list will only display titles that are checked as "Favorite". All titles that are not Favorited are
hidden when this is checked. This is most useful for marking titles to be downloaded later, but can also be used to mark your favorite or most played games.
This box is by default NOT CHECKED when the app starts, so all titles, favorite or not, are displayed.

New - This works like the Favorite filter. When checked, the list will only display titles that have the State "New". All titles that are not "New" are
hidden when this is checked. This is useful to check right after loading new title keys.
This box is by default NOT CHECKED when the app starts, so all titles, new or not, are displayed.

# Sorting

To sort the title list by a column, click the header of the column. Clicking it again will sort in the reverse order. The default sort upon starting the app is alphabetical by Title Name, A - Z, numbers first.

# Downloading

To download a title, expand it and click the "Download Title" button. The downloads window will appear and show you the progress of all files being downloaded. There is a progress bar,
plus the file name, download speed and estimated completion time. For downloads that are completed, you can see when they were completed. Currently downloading files are
always at the top. You can clear completed downloads by pressing the Clear Completed Downloads button. All completed downloads will be cleared when exiting the app. To hide
the Download Window, use Downloads -> Hide Downloads. Double click any active progress bar to cancel the download (does nothing
if the download is completed).

You can also right click the title row and select "Download Title". You can also download the title and all updates, or everything (game, updates and DLC) via the same menu.

The dropdown menu just above Download Title lets you choose what types of titles to download.

By default ony the Base Game will be downloaded. That is version 0, with no updates or DLC. 

To download only updates, select Updates Only from the drop down menu just above. You must choose a version number in the Versions
drop down list. The default selected version is the game's latest version. The selected version will be downloaded, along with
all versions before it (except for version 0, which is the base game).

To download only DLC, select DLC, select DLC Only from the drop down menu. Versions doesn't do anything because the latest version is always used.

To download the base game and all updates, choose Game + Updates. This download base game version 0, plus updates up to the
selected version (see Updates only, above).

To download the base game and all DLC, choose Game + DLC.

To download all updates and DLC, choose Game + DLC. Updates will be downloaded up to the chosen version, as described above.

To download everything, choose Game + Updates + DLC. Updates will be downloaded up to the chosen version, as described above.

Alternatively, you can right click and choose a download option.

There is no limit to the number of downloads you can have going at once, besides your own system and connection. Downloading
many files at once may increase Nintendo's suspicion, however, and may lead to a faster certificate ban. Please use judiciously.

The Downloads menu has several bulk downloading options. Download Favorites downloads all of your unowned favorites. Download Updates
downloads all updates for every game you already own. Download DLC downloads all DLC for every game you own.

Selecting one of those brings up a new menu with four options. The simplest is to download all Alphabetically or by Size. Your entire
queue will be downloaded, assuming you don't close the app, lose your connection or get your cert banned in the meantime. However,
this will only download one title at a time, no matter what. The next title won't start until the previous one is complete and packed. The only difference
is the order - Alphabetically downloads by Title Name (numbers first, Z or non-american last) while By Size downloads the smallest
titles first and the largest titles last.

The special options are the other two. 

Download Smallest will download your queue by size, smallest to largest, but will stop once the titles get beyond a certain size. The app
will display a window asking you for that size after confirming that you want to continue with the download. For example, if you choose "Download Smallest" and enter
"1 GB" (no quotes) into the followup window, all files will be downloaded that are LESS THAN 1 gigabyte (1024 megabytes). 

Download Limited Data does the same as Download Smallest, but instead of limited by the size of the title it downloads by the total
amount of data downloaded so far. For example, if you have titles with the sizes 100MB, 200MB, 300MB, 400MB and so on and you select
"Download Limited Data", then type "1 GB" into the following window. The app will download the first title (100MB total), then the second
title (300 MB total), then the third title (600 MB total), then the fourth title (1000 MB total). When trying to download the fifth title,
it sees that the total would be 1500 MB (which is greater than the limit, 1024 MB) and stop without getting title #5.

If you have a data cap, or can only download so much at once before copying it to another disk, use this function instead of downloading all.

All four options are available for downloading favorites, updates or DLC.

Bulk downloading downloads one title at a time. However, since it starts each title without asking you, you can leave the app
alone to download your whole queue for as long as it needs.

When inputting the limits for Download Smallest and Download Limited Data, use the format of a number followed by unit label.
Number can be any number, including decimals. 1, 10, 100, 2.6, 8.37862356.
Unit label can be bytes, KB, MB, GB, etc up to PB for petabytes (come on though...). Uppercase or lowercase.
1 KB = 1024 bytes, 1 MB = 1024 KB, 1 GB = 1024 MB, 1 TB = 1024 GB
 
Cancel your bulk downloads by using Downloads -> Cancel Bulk Downloads. It won't cancel the current title, but it will prevent any 
additional titles from being downloaded. To cancel the current title, open the Downloads window and double click the progress bar
for the current download.

# Download Queue

A queue of games to download in the future can be created using the existing Favorites and Filtering systems. Rather than
creating a separate list of games, all you need to do is Favorite any games you don't have but want to download later.

To view your download queue, check "Favorites" and "Not Owned" and uncheck everything else, including "Owned". The resulting
list is your queue.

To download your queue, you can either download each one individually as usual or you can use the Downloads -> Download Favorites menu item.

## Menu Functions

# Downloads Menu

The downloads menu lets you see the progress of your downloads and start batch downloads, as well as any other download functions.

# Tools Menu

The tools menu has miscellaneous tools and functions, like updating title keys and updating the certificate.

# Library Menu

The library menu has options for manipulating the contents of the library or its metadata, or saving it manually.

# NSP Menu

The NSP menu contains tools for working with NSPs - packing, unpacking and verifying.

## Miscellaneous

## Known Issues

Closing the downloads window causes serious problems. I have entirely removed the ability to close the window to prevent this.
The only way to close the window is to use Downloads -> Hide Downloads, but it will always come back when clicking Download Title.
I may add an option to not pop up the download window on start.

Bulk downloading updates and dlc is not tested at all. 

## License

???