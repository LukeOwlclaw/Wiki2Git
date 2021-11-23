Wiki2Git
========

Downloads a Wikipedia article and writes it to a Git repository.
Each Wikipedia revision will be converted to a Git commit.
Multiple articles can be combined into the same Git repository by configuring the same out path for multiple Wiki2Git runs.
This is useful for analyzing the changes to an article together with the changes to its discussion page.

Requires `git` on system path.

**Runs considerably faster on Linux than Windows.**

Afterwards [`git_stats generate`](https://github.com/tomgi/git_stats) can be used to generate elaborate statistics.

**Usage:**

    Wiki2Git <string>  /l <string>  [/o <string>]  [/r  <int>]

    Parameters:
    Required:
    <first parameter> : Wikipedia article
    /l                : Language. Available: de, en

    Optional:
    /o : Out path of downloaded article; git repository will be created in sub-folder ./git. Current directory used if not set.
    /r : Start revision. For continuing earlier failed attempt.

For example:

    ./Wiki2Git Plantago_major /l en /o ../plant
