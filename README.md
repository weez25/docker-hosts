# docker-hosts
resolve container name in /etc/hosts

## Usage

```
docker run --name docker-hosts \
    --restart unless-stopped \
    -v /etc/hosts:/etc/hosts \
    -v /var/run/docker.sock:/var/run/docker.sock
    ghcr.io/weez25/docker-hosts:latest
```
