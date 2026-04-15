# Full build (Release)
bash build/build.sh

# Debug build with verbose output  
bash build/build.sh -c Debug -v

# Rebuild bootstrap
bash build/orchestrate.sh Release net10.0 false false

# Show help
bash build/build.sh --help

