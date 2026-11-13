# ---------- Derleme ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS derle
WORKDIR /src
COPY Directory.Build.props ./
COPY UretimPro.Web/UretimPro.Web.csproj UretimPro.Web/
RUN dotnet restore UretimPro.Web/UretimPro.Web.csproj
COPY UretimPro.Web/ UretimPro.Web/
RUN dotnet publish UretimPro.Web/UretimPro.Web.csproj -c Release -o /app --no-restore

# ---------- Çalıştırma ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends fontconfig fonts-dejavu-core tzdata \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=derle /app .

ENV ASPNETCORE_ENVIRONMENT=Production \
    Urls=http://+:8080 \
    VeriKlasoru=/veri \
    TarayiciAc=false \
    TZ=Europe/Istanbul

VOLUME /veri
EXPOSE 8080
ENTRYPOINT ["dotnet", "UretimPro.dll"]
