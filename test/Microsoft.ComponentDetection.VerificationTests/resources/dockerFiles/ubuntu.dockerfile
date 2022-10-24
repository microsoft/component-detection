FROM docker.io/library/ubuntu

LABEL maintainer="foorbar <foobar@microsoft.com>"

ARG TZ="US/Seattle"

ENV TZ ${TZ}
ENV AGENT_PATH /opt/atlassian-agent.jar

COPY atlassian-agent.jar ${AGENT_PATH}
COPY hijack.sh /hijack.sh

RUN set -x \
    && export DEBIAN_FRONTEND=noninteractive \
    && apt update \

CMD ["/hijack.sh"]