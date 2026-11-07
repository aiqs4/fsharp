{
  description = "Nix environment for building the F# compiler";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixpkgs-unstable";

  outputs =
    { self, nixpkgs }:
    let
      system = "x86_64-linux";
      pkgs = import nixpkgs { inherit system; };
      # nixpkgs-unstable only has dotnetCorePackages.sdk_10_0 (10.0.114).
      # global.json requires 10.0.300 and roll-forward does not go backwards,
      # so sdk_10_0_4xx is both missing and the wrong band. 10.0.303 is the
      # newest patch in the 10.0.3xx band that global.json accepts as-is.
      dotnetSdk = pkgs.stdenvNoCC.mkDerivation {
        pname = "dotnet-sdk";
        version = "10.0.303";
        src = pkgs.fetchurl {
          url = "https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.303/dotnet-sdk-10.0.303-linux-x64.tar.gz";
          hash = "sha512-U43xHOudhr3aBhurbt49H21Gu5ugh66QVkPz1Fav6YKUnRuVK86zBaXSLS1uacwOUQAhb/zHCaqoboZ/LZzfmg==";
        };
        dontConfigure = true;
        dontBuild = true;
        dontFixup = true;
        sourceRoot = ".";
        installPhase = ''
          runHook preInstall
          mkdir -p "$out"
          shopt -s dotglob
          mv ./* "$out"/
          runHook postInstall
        '';
      };
    in
    {
      devShells.${system}.ci = pkgs.mkShell {
        packages = with pkgs; [
          bash
          git
          jq
        ];

        shellHook = ''
          export DOTNET_ROOT=${dotnetSdk}
          export PATH="$DOTNET_ROOT:$PATH"
          export DOTNET_CLI_TELEMETRY_OPTOUT=1
          export DOTNET_NOLOGO=1
          export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
        '';
      };
    };
}
