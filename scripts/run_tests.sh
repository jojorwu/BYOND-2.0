#!/bin/bash

# Ensure dotnet is in path
export DOTNET_ROOT=$HOME/.dotnet
export PATH=$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools

SMART_MODE=false
FILTER=""

while [[ "$#" -gt 0 ]]; do
    case $1 in
        --smart) SMART_MODE=true ;;
        *) FILTER="$1" ;;
    esac
    shift
done

if [ "$SMART_MODE" = true ]; then
    echo "Smart mode: Identifying relevant tests based on changes..."
    CHANGES=$(git diff --name-only; git diff --cached --name-only; git status --short | grep '??' | cut -d' ' -f2)

    RELEVANT_TESTS=""

    for FILE in $CHANGES; do
        if [[ $FILE == Core/VM/* ]] || [[ $FILE == Shared/DreamValue.cs ]] || [[ $FILE == Shared/DMReference.cs ]]; then
            RELEVANT_TESTS="$RELEVANT_TESTS DreamVMTests DreamVMVariableTests"
        elif [[ $FILE == Core/Objects/* ]]; then
            RELEVANT_TESTS="$RELEVANT_TESTS GameObjectTests ObjectTypeTests"
        elif [[ $FILE == Server/* ]]; then
            RELEVANT_TESTS="$RELEVANT_TESTS ServerArchitectureTests PlayerManagerTests"
        elif [[ $FILE == Core/Maps/* ]]; then
            RELEVANT_TESTS="$RELEVANT_TESTS MapLoaderTests"
        fi
    done

    # Unique relevant tests
    UNIQUE_TESTS=$(echo $RELEVANT_TESTS | tr ' ' '\n' | sort -u | tr '\n' ' ')

    if [ -z "$UNIQUE_TESTS" ]; then
        echo "No specific relevant tests found for changes. Running all tests."
    else
        echo "Running relevant tests: $UNIQUE_TESTS"
        FILTER=$(echo $UNIQUE_TESTS | sed 's/ /|/g' | sed 's/|$//')
    fi
fi

if [ -n "$FILTER" ]; then
    echo "Executing: dotnet test tests/tests.csproj --filter \"$FILTER\""
    dotnet test tests/tests.csproj --filter "$FILTER"
else
    echo "Executing: dotnet test tests/tests.csproj"
    dotnet test tests/tests.csproj
fi
