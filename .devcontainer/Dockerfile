FROM mcr.microsoft.com/dotnet/sdk:8.0

ENV DOTNET_TOOLS_DIR=/root/.dotnet/tools
ENV PATH="${PATH}:/root/.dotnet/tools"

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates git openssh-client unzip zip \
    && rm -rf /var/lib/apt/lists/* \
    && dotnet tool install --global ilspycmd --version 9.1.0.7988

RUN printf 'Host github.com-private\n    HostName github.com\n' \
    > /etc/ssh/ssh_config.d/github.com-private.conf

WORKDIR /workspaces/NoMoreDispenserLoops
