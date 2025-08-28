# WHAL-Int

## Setup
`Git pull` this repo, `cd ./WHAL-Int` and then run `dotnet restore` and `dotnet run` in the root directory should get you going.

Please put your EID in a separate file called `EID.txt` (this exact filename) in the root directory of the project or the program will exit automatically. This file is ignored by git and thus will not be pushed to any git remotes.

## Usage
In the root folder, run `dotnet run --project WHAL-Int`. Optional parsing values are as follows:
- `--debug`/`-d` - Show debugging (not set up)
- `--reverse`/`-r` - reverses the order of tables
- `--speedrun`/`-sr` - include coops with the "SpeedRun" flag
- `--fastrun`/`-fr` - include coops with the "FastRun" flag
- `--anygrade`/`-ag` - include coops with the "AnyGrade" flag
