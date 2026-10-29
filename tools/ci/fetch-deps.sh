#!/usr/bin/env bash
# Собирает то, против чего компилируется мод, в раскладке, которую ждёт DraftAnimals.csproj (ValheimDir):
#   <dest>/valheim_Data/Managed/        библиотеки игры из бесплатного выделенного сервера Valheim
#                                       (steamcmd, анонимный вход; берётся только Managed, ~35 МБ)
#   <dest>/BepInEx/core/                BepInExPack Valheim (Thunderstore)
#   <dest>/BepInEx/plugins/Jotunn/Jotunn.dll
# Запуск: tools/ci/fetch-deps.sh <dest>, затем dotnet build -c Release -p:ValheimDir=<dest> -p:SkipDeploy=true
# (VALHEIM_SERVER_DIR=<установленный сервер> — без скачивания).
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
source "$here/versions.env"
dest="$(mkdir -p "$1" && cd "$1" && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

if [ ! -f "$dest/valheim_Data/Managed/assembly_valheim.dll" ]; then
  server="${VALHEIM_SERVER_DIR:-}"
  if [ -z "$server" ]; then
    case "$(uname -s)" in
      Darwin) url=https://steamcdn-a.akamaihd.net/client/installer/steamcmd_osx.tar.gz ;;
      *) url=https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz ;;
    esac
    mkdir -p "$work/steamcmd" && curl -sSL "$url" | tar xz -C "$work/steamcmd"
    server="$work/server"
    # на свежей машине steamcmd часто падает с "Missing configuration", следующая попытка проходит
    for try in 1 2 3; do
      "$work/steamcmd/steamcmd.sh" +@sSteamCmdForcePlatformType linux +force_install_dir "$server" \
        +login anonymous +app_update "$VALHEIM_SERVER_APP" validate +quit && break
      [ "$try" = 3 ] && exit 1
      echo "steamcmd failed (try $try), retrying" >&2
    done
  fi
  # у сервера папка называется valheim_server_Data, библиотеки в ней те же, что у игры
  mkdir -p "$dest/valheim_Data"
  rm -rf "$dest/valheim_Data/Managed"
  cp -R "$server/valheim_server_Data/Managed" "$dest/valheim_Data/Managed"
fi

if [ ! -f "$dest/BepInEx/core/BepInEx.dll" ]; then
  curl -sSL -o "$work/bepinex.zip" "https://thunderstore.io/package/download/denikson/BepInExPack_Valheim/$BEPINEX_PACK_VERSION/"
  unzip -q -o "$work/bepinex.zip" -d "$work/bepinex"
  mkdir -p "$dest/BepInEx"
  cp -R "$work/bepinex/BepInExPack_Valheim/BepInEx/core" "$dest/BepInEx/"
fi

if [ ! -f "$dest/BepInEx/plugins/Jotunn/Jotunn.dll" ]; then
  curl -sSL -o "$work/jotunn.zip" "https://thunderstore.io/package/download/ValheimModding/Jotunn/$JOTUNN_VERSION/"
  mkdir -p "$dest/BepInEx/plugins/Jotunn"
  # пути в этом архиве с обратными слешами (plugins\Jotunn.dll), unzip считает их частью имени: ищем по маске
  unzip -p "$work/jotunn.zip" '*Jotunn.dll' > "$dest/BepInEx/plugins/Jotunn/Jotunn.dll"
fi

echo "ValheimDir=$dest"
