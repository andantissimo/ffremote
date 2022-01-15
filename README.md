# FFmpeg remote tools

runs ffmpeg on a remote worker with local files transferred vid HTTP

## Build requirements

* .NET 6 SDK
* GNU Make

## Worker requirements

* FFmpeg
* Reverse proxy (nginx, apache httpd, etc...)

## Installation

if you build on the worker server, simply do:
```sh
make && sudo make install
```

or you can cross-build for your target platform:
```sh
make RID=linux-x64
```

configure your reverse proxy upstream to localhost:5255
```nginx
http {
    map $http_upgrade $connection_upgrade {
        default upgrade;
        ''      close;
    }

    server {
        listen 80;

        location /ffremote/ {
            proxy_pass         http://localhost:5255/;
            proxy_http_version 1.1;
            proxy_buffering    off;
            proxy_set_header   Upgrade    $http_upgrade;
            proxy_set_header   Connection $connection_upgrade;
            proxy_set_header   Host       $http_host;
        }
    }
}
```

## Usage

command options are almost same as ffmpeg, except for `-w` and some unsupported options
```
ffremote [-w http://worker/endpoint] [options] [[infile options] -i infile]... {[outfile options] outfile}...
```

## Troubleshooting

put `/usr/local/etc/ffremote/appsettings.Production.json`
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

and then watch the logs with:
```sh
# Linux
journalctl -fu ffremote -o cat

# FreeBSD/macOS
tail -f /var/log/ffremote.log
````
