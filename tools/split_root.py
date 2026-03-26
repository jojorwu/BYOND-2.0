import os
import re

def split_file(filepath, mapping):
    with open(filepath, 'r') as f:
        content = f.read()

    header_match = re.search(r'^(.*?internal static partial class DreamProcNativeRoot \{)', content, re.DOTALL)
    if not header_match:
        print("Could not find class definition")
        return

    header = header_match.group(1)

    body = content[len(header):].rstrip()
    if body.endswith('}'):
        body = body[:-1].rstrip()

    lines = body.split('\n')
    members = []
    current_member = []
    brace_count = 0
    in_member = False

    for line in lines:
        stripped = line.strip()
        if not in_member:
            if stripped.startswith('[') or 'static' in line or 'public' in line or 'private' in line or 'internal' in line:
                in_member = True

        current_member.append(line)
        brace_count += line.count('{')
        brace_count -= line.count('}')

        if in_member and brace_count == 0:
            if stripped.endswith('}') or stripped.endswith(';'):
                members.append('\n'.join(current_member))
                current_member = []
                in_member = False

    if current_member:
        members.append('\n'.join(current_member))

    files = {category: [header] for category in set(mapping.values())}
    files['Other'] = [header]

    for member in members:
        assigned = False
        for pattern, category in mapping.items():
            if re.search(pattern, member):
                files[category].append(member)
                assigned = True
                break
        if not assigned:
            files['Other'].append(member)

    for category, lines in files.items():
        if len(lines) <= 1: continue

        out_path = filepath.replace('.cs', f'.{category}.cs')
        with open(out_path, 'w') as f:
            f.write('\n'.join(lines))
            f.write('\n}\n')
        print(f"Created {out_path}")

    with open(filepath, 'w') as f:
        f.write(header)
        f.write('\n}\n')
    print(f"Updated {filepath}")

mapping = {
    r'NativeProc_(alert|animate|flick|hearers|ohearers|orange|oview|oviewers|range|view|viewers|stat|statpanel|icon_states|image|isicon|matrix|rgb2num|sound|turn|winclone|winexists|winget|winset|filter)': 'Visual',
    r'(OutputToStatPanel|Rgb2NumBadColor|Rgb2NumBadColorspace)': 'Visual',
    r'NativeProc_(ceil|clamp|floor|fract|lerp|max|min|rand|rand_seed|round|roll|sign|trunc|isinf|isnan|values_cut|values_dot|values_product|values_sum)': 'Math',
    r'(MaxComparison|MinComparison|values_cut_helper|ClampLowerBoundNotANumber|ClampUpperBoundNotANumber|ClampUnexpectedType)': 'Math',
    r'NativeProc_(ascii2text|ckey|ckeyEx|cmptext|cmptextEx|copytext|copytext_char|findtext|findtextEx|findlasttext|findlasttextEx|html_decode|html_encode|list2params|lowertext|nonspantext|num2text|params2list|regex|replacetext|replacetextEx|sorttext|sorttextEx|spantext|spantext_char|splicetext|splicetext_char|splittext|text2ascii|text2ascii_char|text2num|text2path|time2text|trimtext|uppertext|url_decode|url_encode)': 'String',
    r'(_length|NativeProc_length_char|List2Params|Params2List)': 'String',
    r'NativeProc_(block|bounds_dist|get_step_to|get_steps_to|walk|walk_rand|walk_towards|walk_to)': 'Spatial',
    r'(Block)': 'Spatial',
    r'NativeProc_(fcopy|fcopy_rsc|fdel|fexists|file|file2text|flist|ftime|isfile|text2file)': 'IO',
    r'NativeProc_(json_decode|json_encode)': 'Serialization',
    r'(CreateValueFromJsonElement|JsonEncode)': 'Serialization',
    r'NativeProc_(CRASH|generator|hascall|ref|shutdown|sleep|typesof)': 'System',
    r'NativeProc_(isarea|islist|isloc|ismob|isobj|ismovable|isnull|isnum|ispath|istext|isturf)': 'Type',
    r'NativeProc_(md5|sha1)': 'System',
}

split_file('OpenDream/OpenDreamRuntime/Procs/Native/DreamProcNativeRoot.cs', mapping)
