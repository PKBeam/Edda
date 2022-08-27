using System;

public class Edit<T>: ICloneable {
    public bool isAdd;
    public T item;
    public Edit(bool isAdd, T item) {
        this.isAdd = isAdd;
        this.item = item;
    }
    public object Clone() {
        return new Edit<T>(isAdd, item);
    }
}
