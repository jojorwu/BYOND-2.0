import os
import re

def split_file(filepath, mapping):
    with open(filepath, 'r') as f:
        content = f.read()

    header_match = re.search(r'^(.*?public unsafe partial class BytecodeInterpreter\s*\{)', content, re.DOTALL)
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

        if brace_count == 0 and stripped:
             if any(stripped.startswith(prefix) for prefix in ['static', 'private', 'public', 'internal', '[']):
                 if current_member:
                     members.append('\n'.join(current_member))
                     current_member = []
                 in_member = True

        if in_member or brace_count > 0:
            current_member.append(line)
            brace_count += line.count('{')
            brace_count -= line.count('}')

    if current_member:
        members.append('\n'.join(current_member))

    print(f"Found {len(members)} members")

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
    r'CreateDispatchTable': 'Other',
    r'_dispatchTable': 'Other',
    r'Handle(Add|Subtract|Multiply|Divide|Negate|BitAnd|BitOr|BitXor|BitNot|BitShift|Modulus|Power|Sqrt|Abs|Sin|Cos|Tan|Arc|Log)': 'Arithmetic',
    r'Handle(Jump|Call|Return|Try|EndTry|Switch|Throw)': 'ControlFlow',
    r'Handle(PushString|PushFloat|PushNull|Pop|PushProc|PushType|PushResource|NPushFloatAssign|PushNFloats|PushStringFloat)': 'Stack',
    r'Handle(Variable|Field|Local|Argument|Global|Reference|Index|BuiltinVar|RefAndDereferenceField|PushNRefs|NullRef|Assign|Increment|Decrement|AssignInto|AssignNoPush|AssignLocal)': 'Accessors',
    r'Handle(Output|CreateObject|CreateList|CreateAssociativeList|CreateStrictAssociativeList|IsInList|Input|Pick|Dereference|Initial|CreateListEnumerator|Enumerate|DestroyEnumerator|Append|Remove|DeleteObject|Prob|IsSaved|GetStep|GetDist|GetDir|MassConcatenation|FormatString|Locate|Length|Spawn|Rgb|Gradient)': 'Engine',
    r'Handle(Compare|IsNull|IsType|IsInRange)': 'Comparison',
    r'Handle(BooleanNot|BooleanAnd|BooleanOr)': 'ControlFlow',
}

split_file('Engine/Core/VM/Runtime/BytecodeInterpreter.Opcodes.cs', mapping)
