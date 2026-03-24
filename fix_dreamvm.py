import re
path = 'Engine/Core/VM/Runtime/DreamVM.cs'
with open(path, 'r') as f:
    content = f.read()

pattern = re.compile(r'protected override Task OnInitializeAsync\(\)\s*\{.*?\}', re.DOTALL)
replacement = """protected override Task OnInitializeAsync()
    {
        Initialize();
        return Task.CompletedTask;
    }"""
# Wait, DreamVM might have used public override Task InitializeAsync initially.
content = content.replace('public override Task InitializeAsync()', 'protected override Task OnInitializeAsync()')
content = pattern.sub(replacement, content)
with open(path, 'w') as f:
    f.write(content)
