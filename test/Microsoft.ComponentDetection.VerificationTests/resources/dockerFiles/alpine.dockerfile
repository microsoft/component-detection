FROM docker.io/library/alpine:latest@sha256:1304f174557314a7ed9eddb4eab12fed12cb0cd9809e4c28f29af86979a3c870

LABEL maintainer="foobar <foobar@microsoft.com"

ARG TZ="US/Seattle"

ENV TZ ${TZ}

RUN apk upgrade \
    && apk add bash tzdata bind-tools busybox-extras ca-certificates libc6-compat wget curl

CMD ["/bin/bash"]