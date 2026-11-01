# LandmarkEmu (G1) — make 管理 docker compose(照 akk-stack 的用法)
#
#   make build world     git pull → 重建並啟動 world → 等服務真的起來 → 列出狀態
#   make build           同上，全部服務
#   make help            列出所有指令
#
# 只操作這個 compose 專案(預設 landmarkemu-ref);down 不加 -v,不做任何 prune。

SHELL        := /bin/bash
PROJECT      ?= landmarkemu-ref
DC           := docker compose -p $(PROJECT)
WAIT         ?= 600
ACCOUNT_PORT := $(shell sed -n 's/^ACCOUNT_PORT=//p' .env 2>/dev/null | tr -d '\r"')
ACCOUNT_PORT := $(if $(ACCOUNT_PORT),$(ACCOUNT_PORT),5000)

# 指令後面可以接服務名稱(make build world、make logs auth)。
SERVICES := setup icons db auth web world social commerce envserver
SVC      := $(filter $(SERVICES),$(MAKECMDGOALS))
LONG_RUNNING := db auth web world social commerce envserver
TARGETS  := $(if $(SVC),$(filter $(LONG_RUNNING),$(SVC)),db auth web world)

.DEFAULT_GOAL := help
.PHONY: help build up down restart ps logs pull wait info $(SERVICES)

##@ 部署

build: pull ## git pull + 重建並啟動(可指定服務),等服務起來後列出狀態
	$(DC) up -d --build $(SVC)
	@$(MAKE) --no-print-directory wait TARGETS="$(TARGETS)"
	@$(DC) ps
	@echo "commit: $$(git log --oneline -1) [$$(git rev-parse --abbrev-ref HEAD)]"

pull: ## git pull(只接受 fast-forward),並顯示目前 commit
	git pull --ff-only
	@echo "commit: $$(git log --oneline -1) [$$(git rev-parse --abbrev-ref HEAD)]"

##@ 服務

up: ## 啟動(不重建,可指定服務),等服務起來
	$(DC) up -d $(SVC)
	@$(MAKE) --no-print-directory wait TARGETS="$(TARGETS)"
	@$(DC) ps

down: ## 停止並移除容器(資料庫 volume 保留，不加 -v)
	$(DC) down

restart: ## 重啟(可指定服務),等服務起來
	$(DC) restart $(SVC)
	@$(MAKE) --no-print-directory wait TARGETS="$(TARGETS)"
	@$(DC) ps

ps: ## 列出容器狀態
	@$(DC) ps

logs: ## 追 log(預設 world;make logs auth)
	$(DC) logs -f --tail 200 $(if $(SVC),$(SVC),world)

##@ 檢查

wait: ## 等指定服務 running 且穩定(有 healthcheck 的要 healthy),逾時或重啟就失敗
	@deadline=$$(( $$(date +%s) + $(WAIT) )); \
	for s in $(TARGETS); do \
	  id=$$($(DC) ps -q $$s 2>/dev/null); \
	  if [ -z "$$id" ]; then echo "⚠ $$s:沒有容器(未啟用?),略過"; continue; fi; \
	  r0=$$(docker inspect -f '{{.RestartCount}}' $$id); stable=0; \
	  printf '… 等待 %s ' "$$s"; \
	  while :; do \
	    st=$$(docker inspect -f '{{.State.Status}}' $$id); \
	    hc=$$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' $$id); \
	    rc=$$(docker inspect -f '{{.RestartCount}}' $$id); \
	    if [ "$$rc" != "$$r0" ] || [ "$$st" = exited ] || [ "$$st" = dead ] || [ "$$hc" = unhealthy ]; then \
	      echo; echo "✗ $$s 啟動失敗(狀態 $$st、health $$hc、重啟 $$r0→$$rc)。最後 40 行 log:"; \
	      $(DC) logs --tail 40 $$s; exit 1; fi; \
	    if [ "$$st" = running ] && { [ "$$hc" = healthy ] || { [ "$$hc" = none ] && [ $$stable -ge 15 ]; }; }; then \
	      echo "✓ ($$hc)"; break; fi; \
	    [ "$$st" = running ] && [ "$$hc" = none ] && stable=$$((stable+5)); \
	    if [ $$(date +%s) -ge $$deadline ]; then \
	      echo; echo "✗ $$s 等了 $(WAIT) 秒還沒起來(狀態 $$st、health $$hc)。"; \
	      $(DC) logs --tail 40 $$s; exit 1; fi; \
	    printf '.'; sleep $${SLEEP:-5}; \
	  done; \
	done; \
	if echo " $(TARGETS) " | grep -q ' auth '; then \
	  if curl -fsS -m 5 http://127.0.0.1:$(ACCOUNT_PORT)/info >/dev/null; then echo "✓ /info 有回應(port $(ACCOUNT_PORT))"; \
	  else echo "⚠ /info 沒有回應(port $(ACCOUNT_PORT)),請看 make logs auth"; fi; \
	fi; \
	echo "服務已正常啟動完成。"

info: ## 目前 commit、容器狀態、/info
	@echo "commit: $$(git log --oneline -1) [$$(git rev-parse --abbrev-ref HEAD)]"
	@$(DC) ps
	@curl -fsS -m 5 http://127.0.0.1:$(ACCOUNT_PORT)/info && echo || echo "/info 沒有回應(port $(ACCOUNT_PORT))"

##@ 其他

help: ## 列出所有指令
	@awk 'BEGIN{FS=":.*## "} /^##@/{printf "\n\033[1m%s\033[0m\n", substr($$0,5)} /^[a-z-]+:.*## /{printf "  \033[36mmake %-9s\033[0m %s\n", $$1, $$2}' $(MAKEFILE_LIST)
	@echo; echo "服務名稱可接在 build / up / restart / logs 後面:$(SERVICES)"

# 讓 "make build world" 裡的 world 不被當成另一個目標。
$(SERVICES):
	@:
