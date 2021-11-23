folder=$(echo $1 | sed 's/[^a-zA-Z0-9]//g')
echo Processing article $1 and saving to $folder

./Wiki2Git $1 /l de /o ../$folder
./Wiki2Git Diskussion:$1 /l de /o ../$folder
cd ../$folder/git

git_stats generate
cd ../..