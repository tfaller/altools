import { OutputChannel, window } from "vscode"

/**
 * Internal details to keep track of the pooled channels.
 */
interface ChannelDetails {
    channel: OutputChannel
    done: boolean
    disposed: boolean
    number: number
}

const pools: Record<string, ChannelDetails[]> = {}

export interface PooledOutputChannel extends OutputChannel {
    /**
     * Marks the channel as done. The channel will be automatically disposed
     * when a new channel is created.
     */
    done(): void
}

/**
 * Similar to the `window.createOutputChannel` method, but will dispose previously done channels with the same name.
 * If multiple active channels exist, the name will be suffixed with "#<top-activ-channel-number + 1>".
 * @param name Name of the output channel. This name is used to group the channels.
 * @returns A PooledOutputChannel
 */
export const createOutputChannel = (name: string): PooledOutputChannel => {
    let topUsedNumber = 0

    const pool = pools[name] = pools[name]?.filter(({ channel, done, disposed, number }) => {
        if (done) {
            disposed || channel.dispose()
            return false
        }
        topUsedNumber = Math.max(topUsedNumber, number)
        return true
    }) ?? []

    topUsedNumber++

    const channel = window.createOutputChannel(topUsedNumber === 1 ? name : `${name} #${topUsedNumber}`)

    const detail = {
        channel,
        done: false,
        disposed: false,
        number: topUsedNumber
    }

    const pooledChannel = {
        ...channel,

        done: () => {
            detail.done = true
        },

        dispose: () => {
            if (detail.disposed) return
            detail.disposed = detail.done = true
            channel.dispose()
        }
    }

    pool.push(detail)
    return pooledChannel
}