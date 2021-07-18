Wiki2Git
========

Downloads a Wikipedia article and writes it to a Git repository.
Each Wikipedia revision will be converted to a Git commit.

Requires `git` on system path.

Runs considerably faster on Linux than Windows.

Afterwards [`git_stats generate`](https://github.com/tomgi/git_stats) can be used to generate elaborate statistics.

**Usage:**

    Wiki2Git <string>  /l <string>  [/o <string>]  [/r  <int>]

    Parameters:
    Required:
    <first parameter> : Wikipedia article
    /l                : Language. Available: de, en

    Optional:
    /o : Out path. Current directory if not set.
    /r : Start revision. For continuing early failed attempt.

