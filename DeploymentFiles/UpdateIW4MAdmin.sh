#!/bin/bash
echo "======================================="
echo " IW4MAdmin Updater v1                  "
echo "======================================="

while getopts scvd: flag
do
    case "${flag}" in
        s) silent='true';;
        c) clean='true';;
        v) verified='true';;
        d) directory=${OPTARG};;
        *) exit 1;;
    esac
done

start=$SECONDS
repoName="RaidMax/IW4M-Admin"
releaseUri="https://api.github.com/repos/$repoName/releases"

echo "Retrieving latest version info..."

if [ ! "$directory" ]
then
  directory=$(pwd)
else
  if [ ! -d "$directory" ]
    then
      mkdir "$directory"
  fi
fi

if [ "$verified" ]
then
  releaseUri="https://api.github.com/repos/$repoName/releases/latest"
fi

releaseInfo=$(curl -s "${releaseUri}")
downloadUri=$(echo "$releaseInfo" | grep "browser_download_url" | cut -d '"' -f 4"" | head -n1)
publishDate=$(echo "$releaseInfo"| grep "published_at" | cut -d '"' -f 4"" | head -n1)
releaseTitle=$(echo "$releaseInfo" | grep "tag_name" | cut -d '"' -f 4"" | head -n1)
filename=$(basename $downloadUri)
fullpath="$directory/$filename"

echo "The latest version is $releaseTitle released $publishDate"

if [[ ! "$silent" ]]
  then
  echo -e "\033[33mAll IW4MAdmin files will be updated.\033[0m"
  echo -e "\033[33mYour database and configuration will not be modified.\033[0m"
  read -p "Are you sure you want to continue [Y/N]? " -n 1 -r
  echo 
  if ! [[ $REPLY =~ ^[Yy]$ ]]
  then
     exit 0
  fi
fi

echo "Downloading update. This might take a moment..."

wget -q "$downloadUri" -O "$fullpath"

if [[ $? -ne 0 ]]
then
  echo "Could not download update files!"
  exit 1
fi

echo "Extracting $filename to $directory"

unzip -o -q "$fullpath" -d "$directory"

if [[ $? -ne 0 ]]
then
  echo "Could not extract update files!"
  exit 1
fi

if [[ "$clean" ]]
then
  echo "Running post-update clean..."
  cat "_delete.txt" | while read -r line || [[ -n $line ]];
  do
    rm -f "$directory/$line"
    if [[ $? -ne 0 ]]
    then
      echo "Could not clean $directory/$line!"
      exit 1
    fi
  done
fi

echo "Removing temporary files..."
rm -f "$fullpath"

if [[ $? -ne 0 ]]
then
  echo "Could not remove update files!"
  exit 1
fi

chmod +x "$directory/StartIW4MAdmin.sh"

executionTime=$(($SECONDS - start))
echo "Update completed successfully in $executionTime seconds!"
