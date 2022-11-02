FROM docker.io/library/atlassian/confluence-server@sha256:1552dbec533e1793d8b3c8459dac43d71faaa0bfe01980961db19c091a850113

LABEL maintainer="foobar <foobar@microsoft.com>"

ARG TZ="US/Seattle"

ENV TZ ${TZ}
ENV AGENT_PATH /opt/atlassian-agent.jar

COPY atlassian-agent.jar ${AGENT_PATH}
COPY hijack.sh /hijack.sh

RUN set -x \
    && export DEBIAN_FRONTEND=noninteractive \
    && apt update \

CMD ["/hijack.sh"]