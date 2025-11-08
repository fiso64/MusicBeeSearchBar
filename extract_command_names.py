# This script is used to generate the user-friendly command name mappings in ApplicationCommand.cs

import re
import argparse
import sys
import os

def extract_command_mappings(file_path):
    """
    Extracts mappings from ApplicationCommand.command to string names
    from a C# file.

    The pattern looks for lines like:
    new ANY_TYPE_NAME(ANY_CALL_EXPRESSION("some_string", "Actual Name"), ApplicationCommand.CommandName);
    where ANY_TYPE_NAME and ANY_CALL_EXPRESSION can be complex/obfuscated.

    Args:
        file_path (str): Path to the C# file.

    Returns:
        dict: A dictionary mapping command names (without ApplicationCommand. prefix)
              to their C#-escaped string names.
              Returns an empty dict if the file cannot be read or no matches are found.
    """
    command_mappings = {}

    # Regex breakdown:
    # new\s+[^()\s,"]+\s*\(\s*[^()\s,"]+\s*  # Matches 'new ObfuscatedClass( ObfuscatedMethodCall'
    # \(                                    # Matches the opening parenthesis of the inner method call
    #   (?:                                 # Start of a non-capturing group for preceding arguments
    #     \s*".*?"\s*,\s*                   # Matches a full string literal (e.g., "arg1") followed by a comma
    #   )*                                  # This group (an arg + comma) can appear zero or more times
    #   \s*"(.*?)"                          # CAPTURE GROUP 1: Matches the LAST string literal (the name we want)
    # \s*\)                                 # Matches the closing parenthesis of the inner method call
    # \s*,\s*                               # Matches the comma separating the two main arguments
    # (ApplicationCommand\.[\w]+)           # CAPTURE GROUP 2: Matches the ApplicationCommand
    # \s*\)\s*;                             # Matches the final closing parenthesis and semicolon

    regex_pattern = r'new\s+[^()\s,"]+\s*\(\s*[^()\s,"]+\s*\((?:\s*".*?"\s*,\s*)*\s*"(.*?)"\s*\)\s*,\s*(ApplicationCommand\.[\w]+)\s*\)\s*;'

    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
    except FileNotFoundError:
        print(f"Error: File not found at {file_path}", file=sys.stderr)
        return {}
    except Exception as e:
        # Ignore errors from files that are not text-based
        if isinstance(e, UnicodeDecodeError):
            return {}
        print(f"Error reading file {file_path}: {e}", file=sys.stderr)
        return {}

    matches = re.findall(regex_pattern, content)

    for match in matches:
        string_name_raw = match[0]  # Content of the C# string literal as captured by regex
        app_command_full = match[1] # e.g., "ApplicationCommand.CommandName"
        
        # Strip leading/trailing whitespace from the raw string name
        stripped_name = string_name_raw.strip()

        # Escape characters for C# string literal compatibility.
        # Backslashes must be escaped before double quotes.
        # e.g., 'path C:\Users' -> 'path C:\\Users'
        # e.g., 'Name "User"' -> 'Name \"User\"'
        string_name_escaped = stripped_name.replace('\\', '\\\\').replace('"', '\\"')
        
        # Extract the command name part (e.g., "CommandName" from "ApplicationCommand.CommandName")
        # Regex (ApplicationCommand\.[\w]+) ensures there's a dot and \w+ after it.
        command_name_only = app_command_full.split('.')[-1]
        
        command_mappings[command_name_only] = string_name_escaped

    return command_mappings

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Extract ApplicationCommand mappings from a C# file or directory.")
    parser.add_argument("path", help="Path to the C# file or directory to scan recursively")
    args = parser.parse_args()

    all_mappings = {}
    input_path = args.path

    if os.path.isfile(input_path):
        # Handle the case where a single file is provided
        all_mappings = extract_command_mappings(input_path)
    elif os.path.isdir(input_path):
        # Handle the case where a directory is provided
        print(f"// Scanning directory: {input_path}", file=sys.stderr)
        for root, _, files in os.walk(input_path):
            for filename in files:
                if filename.endswith(".cs"):
                    file_path = os.path.join(root, filename)
                    mappings_from_file = extract_command_mappings(file_path)
                    all_mappings.update(mappings_from_file) # Merge dictionaries
    else:
        print(f"Error: Path '{input_path}' does not exist or is not a valid file/directory.", file=sys.stderr)
        sys.exit(1)


    if all_mappings:
        output_lines = []
        output_lines.append("new Dictionary<ApplicationCommand, string>()")
        output_lines.append("{")
        
        # Sort by key for consistent output
        for command_key, string_name_value in sorted(all_mappings.items()):
            # command_key is the command name, e.g., "MyCommand"
            # string_name_value is the C#-escaped string, e.g., "Actual \\"Name\\" for display"
            # C# output for key: MyCommand (no quotes, as it's an enum member)
            # C# output for value: "Actual \"Name\" for display" (with quotes, as it's a string literal)
            output_lines.append(f"    {{ ApplicationCommand.{command_key}, \"{string_name_value}\" }},")
            
        output_lines.append("};")
        print("\n".join(output_lines))
    else:
        # This message goes to stdout, errors from the script go to stderr.
        print("// No mappings found or an error occurred.")