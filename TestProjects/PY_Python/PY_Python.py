import os
import sys

# Display command line arguments
print("Command Line Arguments:")
for arg in sys.argv:
    print(arg)

# Display environment variables
print("\nEnvironment Variables:")
for key, value in os.environ.items():
    print(f"{key} = {value}")
