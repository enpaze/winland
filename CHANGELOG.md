# Changelog

## [0.5.0](https://github.com/enpaze/winland/compare/v0.4.0...v0.5.0) (2026-11-21)


### ⚠ BREAKING CHANGES

* **config:** drop Hyprland-only config syntax ([#524](https://github.com/enpaze/winland/issues/524))
* **cli:** tweaks off and tweaks restore subcommands ([#523](https://github.com/enpaze/winland/issues/523))
* **config:** flat option names ([#521](https://github.com/enpaze/winland/issues/521))
* **config:** bind a chord with one bind keyword ([#520](https://github.com/enpaze/winland/issues/520))
* **actions:** short action names and one direction vocabulary ([#519](https://github.com/enpaze/winland/issues/519))
* **cli:** one CLI namespace ([#518](https://github.com/enpaze/winland/issues/518))
* **ipc:** camelCase JSON replies with only keys that vary ([#517](https://github.com/enpaze/winland/issues/517))
* **events:** one event per change, a focus event, and JSON events ([#512](https://github.com/enpaze/winland/issues/512))
* **cli:** name mouse buttons and answer empty or drag-only commands plainly ([#509](https://github.com/enpaze/winland/issues/509))
* **config:** a mouse key with a move or resize action is a drag ([#508](https://github.com/enpaze/winland/issues/508))
* **selector:** match window selectors like window rules ([#507](https://github.com/enpaze/winland/issues/507))
* **cli:** answer winland version without a running Winland ([#506](https://github.com/enpaze/winland/issues/506))
* simplify window rules to exe, class, and title ([#489](https://github.com/enpaze/winland/issues/489))
* keep only the agreed Options ([#479](https://github.com/enpaze/winland/issues/479))
* the config file is the only way to configure ([#478](https://github.com/enpaze/winland/issues/478))
* remove the Dispatchers and bind features the starter never uses ([#477](https://github.com/enpaze/winland/issues/477))
* make Winland easier to configure, inspect and control ([#460](https://github.com/enpaze/winland/issues/460))

### Features

* **actions:** short action names and one direction vocabulary ([#519](https://github.com/enpaze/winland/issues/519)) ([af78ac2](https://github.com/enpaze/winland/commit/af78ac2ec941850ff2f1eeb150e1bc6d7038da98))
* **cli:** answer winland version without a running Winland ([#506](https://github.com/enpaze/winland/issues/506)) ([cd59a7e](https://github.com/enpaze/winland/commit/cd59a7ee541002bbbb669a12485b6ad512de182c))
* **cli:** one CLI namespace ([#518](https://github.com/enpaze/winland/issues/518)) ([4c2c86f](https://github.com/enpaze/winland/commit/4c2c86ff0dc049bfac1d99870ca581c26bff00c4))
* **cli:** tweaks off and tweaks restore subcommands ([#523](https://github.com/enpaze/winland/issues/523)) ([ed62dc7](https://github.com/enpaze/winland/commit/ed62dc7c0c9e1c449abdde9ea3de88cfd45e0b75))
* **cli:** winland files names the config read and the log written ([#446](https://github.com/enpaze/winland/issues/446)) ([c8edcde](https://github.com/enpaze/winland/commit/c8edcdec6cbcc45a3993334461d09270dfafc009))
* **config:** a mouse key with a move or resize action is a drag ([#508](https://github.com/enpaze/winland/issues/508)) ([51afe4f](https://github.com/enpaze/winland/commit/51afe4feaaac3dcd68e69ad63780e123ab0c9a31))
* **config:** bind a chord with one bind keyword ([#520](https://github.com/enpaze/winland/issues/520)) ([726659a](https://github.com/enpaze/winland/commit/726659aff111823a7a458fc68cf7eb3e30f2b67d))
* **config:** flat option names ([#521](https://github.com/enpaze/winland/issues/521)) ([2c022a7](https://github.com/enpaze/winland/commit/2c022a733b9e56e1ea0bc3c92d695ed7bb960609))
* **config:** hold an exec-once window on [workspace N] ([#510](https://github.com/enpaze/winland/issues/510)) ([27dea79](https://github.com/enpaze/winland/commit/27dea796a21424db63b722e706438ebd2a457bd3))
* **config:** several effects per window rule ([#516](https://github.com/enpaze/winland/issues/516)) ([a969b70](https://github.com/enpaze/winland/commit/a969b70f18d41bc5801ecf576f11c27c0aa07616))
* **events:** one event per change, a focus event, and JSON events ([#512](https://github.com/enpaze/winland/issues/512)) ([ae09c36](https://github.com/enpaze/winland/commit/ae09c36227d632285289563320f7b0b4e89c76ac))
* keep only the agreed Options ([#479](https://github.com/enpaze/winland/issues/479)) ([c1a903b](https://github.com/enpaze/winland/commit/c1a903b5164452edb563d277732c55a0132010dd))
* make Winland easier to configure, inspect and control ([#460](https://github.com/enpaze/winland/issues/460)) ([f36424e](https://github.com/enpaze/winland/commit/f36424e2a5172394643c94c69f10a568e1f9a75f))
* remove the Dispatchers and bind features the starter never uses ([#477](https://github.com/enpaze/winland/issues/477)) ([ba2c398](https://github.com/enpaze/winland/commit/ba2c39888cb9f5ae45d491346e26282a11a4c4a1))
* **selector:** match window selectors like window rules ([#507](https://github.com/enpaze/winland/issues/507)) ([a963b1e](https://github.com/enpaze/winland/commit/a963b1e2857de203252246e7b26b80d8f31dd13d))
* simplify window rules to exe, class, and title ([#489](https://github.com/enpaze/winland/issues/489)) ([d98b74d](https://github.com/enpaze/winland/commit/d98b74d071cd5123d78b918fca41043dd5503281))
* the config file is the only way to configure ([#478](https://github.com/enpaze/winland/issues/478)) ([60bcd04](https://github.com/enpaze/winland/commit/60bcd04771b6bc307035d048d8ca68f3cf337c05))


### Bug Fixes

* a Store app's frame is adopted when its app joins it late ([#453](https://github.com/enpaze/winland/issues/453)) ([b1ce220](https://github.com/enpaze/winland/commit/b1ce220f4d741f3beab7f6e01cfe206dfe11e302))
* a tray flyout opens where its program put it ([#452](https://github.com/enpaze/winland/issues/452)) ([43eb031](https://github.com/enpaze/winland/commit/43eb0315338df0587deaea7b21f1ef1b96e47a37))
* bugs, slow paths and border glitches found in a full review ([#445](https://github.com/enpaze/winland/issues/445)) ([79b3264](https://github.com/enpaze/winland/commit/79b32641a09b5bc0e9816c6942daa8e6b5eb9a59))
* **ci:** remove manual trigger flagged by CodeQL ([#496](https://github.com/enpaze/winland/issues/496)) ([d6c2096](https://github.com/enpaze/winland/commit/d6c2096650bd962973673829ec25f091248aa0c9))
* **cli:** name mouse buttons and answer empty or drag-only commands plainly ([#509](https://github.com/enpaze/winland/issues/509)) ([d6776ba](https://github.com/enpaze/winland/commit/d6776ba3f69d8a68a3bc5e67d31ea04b744b28dd))
* delete retired install files once they are unlocked ([#502](https://github.com/enpaze/winland/issues/502)) ([194ee7c](https://github.com/enpaze/winland/commit/194ee7c94a74d05d53d69967a5c3f2b2f1100507))
* **drag:** keep the size a floating window gives itself ([#515](https://github.com/enpaze/winland/issues/515)) ([3c708d9](https://github.com/enpaze/winland/commit/3c708d95029a21e7c96378d578ceb413876b7346))
* draw a workspace slide only on its own monitor ([#525](https://github.com/enpaze/winland/issues/525)) ([c7decd7](https://github.com/enpaze/winland/commit/c7decd72c328e7cccfaa83112805eca0c8f1aa6e))
* include the native launcher in release packages ([#450](https://github.com/enpaze/winland/issues/450)) ([93da158](https://github.com/enpaze/winland/commit/93da158bfb330bf9b14a9c67ca9e1d097a263139))
* keep a workspace slide off the other monitor ([#527](https://github.com/enpaze/winland/issues/527)) ([d8f9d03](https://github.com/enpaze/winland/commit/d8f9d034070f90e800e3eb8708c11ab4de03247a))
* keep hung windows from blocking startup recovery ([#449](https://github.com/enpaze/winland/issues/449)) ([daae66e](https://github.com/enpaze/winland/commit/daae66e44c56d02a87e276c89a5285e0ecca4c55))
* keep the launcher from opening a console ([#513](https://github.com/enpaze/winland/issues/513)) ([a18dd31](https://github.com/enpaze/winland/commit/a18dd3123d901e4e5e55caf1130ad22b65914289))
* leave transparent always-on-top indicators unmanaged ([#448](https://github.com/enpaze/winland/issues/448)) ([3e42f66](https://github.com/enpaze/winland/commit/3e42f6605f9b31c674e068181d443a4266e53d58))
* retire only renamed install files ([#504](https://github.com/enpaze/winland/issues/504)) ([f348884](https://github.com/enpaze/winland/commit/f348884fb079850c2a8014f18b5999fa55abec19))
* **tweaks:** turn Snap back on when restoring it ([204edff](https://github.com/enpaze/winland/commit/204edffbea6e4f063c00f9130563bc30a5e54b53))
* **windows:** send a busy neighbour the newest live resize step it skipped ([#514](https://github.com/enpaze/winland/issues/514)) ([3092076](https://github.com/enpaze/winland/commit/3092076a39a90b6a3c5fbfdf4fe0031bbf07bdac))


### Code Refactoring

* **config:** drop Hyprland-only config syntax ([#524](https://github.com/enpaze/winland/issues/524)) ([a376493](https://github.com/enpaze/winland/commit/a37649345378c0c7c4cb5389efe38b43be7ef051))
* **ipc:** camelCase JSON replies with only keys that vary ([#517](https://github.com/enpaze/winland/issues/517)) ([dfc1037](https://github.com/enpaze/winland/commit/dfc1037fb2140443a63b72deb8b1760144aad963))
