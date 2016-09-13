# httpd

Simple async http server written in F#. Supports only HEAD and GET methods.

## Build (Linux)

```
sudo apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF
echo "deb http://download.mono-project.com/repo/debian wheezy main" | sudo tee /etc/apt/sources.list.d/mono-xamarin.list
sudo apt-get update
sudo apt-get install mono-complete fsharp
git clone https://github.com/Ustimov/httpd.git
cd httpd


xbuild httpd.sln /p:Configuration=Release
cd httpd/bin/Release
chmod +x httpd.exe

```

## Run

```
sudo ./httpd.exe [-a IP_ADDRESS] [-p PORT] [-r DOCUMENT_ROOT]
```

By default server starts at 127.0.0.1:80 with current directory as DOCUMENT_ROOT.


