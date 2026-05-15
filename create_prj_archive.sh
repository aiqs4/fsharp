#!/usr/bin/env fish

# 1. Liste der Ordner definieren
set folders \
    .

    # \
    # src/FSharp.Core

# 2. Zieldatei definieren
set output_file "combined_projects.txt"

# Liste der auszu schließenden Ordnernamen (getrennt durch Pipe |)
set exclude_pattern "(/bin/|/obj/|docs/|/node_modules/|artifact.*/|.dotnet.*/)"

# Bestehende alte Datei löschen
rm -f $output_file

echo "=== CODE BUNDLE START ===" > $output_file

# 3. Ordner durchlaufen und Code in eine Datei schreiben
for folder in $folders
    # Prüfen, ob der Ordner überhaupt existiert
    if test -d $folder
        # Alle passenden Dateien finden
        for file in $folder/**/*.{fsproj,slnx,sh,props,targets}
            # AUSSCHLUSS-FILTER: Prüft, ob der Pfad ein verbotenes Verzeichnis enthält
            if string match -r -q $exclude_pattern $file
                continue
            end

            # Sicherstellen, dass es eine reguläre Datei ist
            if test -f $file
                echo "" >> $output_file
                echo "========================================" >> $output_file
                echo "FILE: $file" >> $output_file
                echo "========================================" >> $output_file
                echo "" >> $output_file

                # Inhalt der Datei anhängen
                cat $file >> $output_file
            end
        end
    end
end

echo "Fertig! Alle Dateien zusammengefügt in: $output_file"
