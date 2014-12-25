#!/bin/bash

mv ./docs/output ./output

for x in $(ls -a . | grep -v '^\(output\|\.git\|\.\.\?\)$'); do
  rm -r ${x}
done

mv ./output/* ./

rmdir ./output

