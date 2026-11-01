#!/bin/sh
# Brings an older client's data tables to the columns this server reads (0.522: four tables lack the last columns, two
# have blank numbers). A table that already has the columns is left alone, so a 0.695 game folder is not touched.
# Usage: fit-tables.sh <folder>
set -e

fit() { # table, columns, values for the missing columns, column whose blanks become 0 (0 = none)
    file="$1/$2.txt"
    [ -f "$file" ] || return 0
    awk -F'^' -v OFS='^' -v want="$3" -v extra="$4" -v zero="$5" '
        {
            sub(/\r$/, "")
            if (length($0) == 0) next
            if (FNR == 1) { n = NF - 1; if (n >= want) { skip = 1 } }
            if (skip) { print; next }
            if (FNR == 1) { printf "%s%s^\n", $0, names(want - n); next }
            if (zero && $zero == "") $zero = "0"
            printf "%s%s^\n", $0, extra
        }
        function names(count,   s, i) { for (i = 1; i <= count; i++) s = s "ADDED_" i "^"; sub(/\^$/, "", s); return s }
    ' "$file" > "$file.tmp"
    mv "$file.tmp" "$file"
}

#   table               columns  added (string first where the model has one)  blanks
fit "$1" CraftingStations    17  '^0^0^0'                                        0
fit "$1" ProfileDefinitions  51  '0'                                             0
fit "$1" PropDefs            11  '0^0'                                           2
fit "$1" Recipes             18  '0^0^0^0'                                       11
