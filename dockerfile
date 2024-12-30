# Use the official .NET SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["gainz-bot/gainz-bot/gainz-bot.csproj", "./"]
RUN dotnet restore "./gainz-bot.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet clean "gainz-bot.csproj" -c Release
RUN dotnet build "gainz-bot.csproj" -c Release -o /app/build
RUN dotnet publish "gainz-bot.csproj" -c Release -o /app/publish --self-contained -r linux-x64 /p:PublishSingleFile=true /p:PublishTrimmed=true

# Use a smaller base image for the final stage
FROM mcr.microsoft.com/dotnet/runtime-deps:6.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY gainz-bot/gainz-bot/Secrets /app/Secrets
ENV GOOGLE_APPLICATION_CREDENTIALS="/app/Secrets/firebase-credz.json"
ENTRYPOINT ["./gainz-bot"]
