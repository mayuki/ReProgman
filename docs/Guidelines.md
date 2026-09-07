## Repository layout
Create `src` and `test` folders under the repository root, and put the projects
inside them.

- repository root
	- .gitignore
	- App.slnx
	- docs
	- src
		- App.Model
	- test
		- App.Model.Tests
## Project rules
- Keep the tree in a state where it always builds, runs and can be checked
- Keep the docs up to date so the developer can follow the progress
- - .NET CLI tools must not be installed globally
- Commit messages follow the conventional commit format
- Dependencies must be licensed under MIT, Apache 2.0, BSD, ISC, MPL or be in the
  public domain
  - Exceptions may be granted for proprietary licenses such as the Windows SDK,
    or for LGPL packages that come in as a direct or transitive dependency of a
    development tool. Ask the user whenever one of those is needed

## Documentation
- When the implementation and the docs disagree, write the change back into the
  docs
- Before committing, review whether anything new needs to be specified and write
  it into the docs

## Coding rules
- Practice test-driven development (always Red → Green)
- Code says How, tests say What, comments say Why, and code comments say Why not
- Always confirm that the build and the tests pass after a change
  - This may be skipped for docs-only changes
- Write code comments in English
